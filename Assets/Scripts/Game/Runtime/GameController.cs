using System.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Inventory;
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
    public sealed partial class GameController : MonoBehaviour
    {
        public const ulong DefaultSeed = 20261028;
        public static readonly Money StartingCash = Money.FromYuan(15000);

        public GameSession Session { get; private set; }
        public CityLayout City { get; private set; }
        public StreetWorldBuilder World { get; private set; }
        public ShopInteriorView Interior { get; private set; }
        public Camera Cam { get; private set; }
        public GameUi Ui { get; private set; }
        public HomeRoomView HomeRoom { get; private set; }
        public HomeRoomView VisitRoom { get; private set; }
        public bool InVisit => Session?.CurrentPlace?.Kind == PlaceKind.Home && !InHome;
        private HomeRoomView ActiveResidence => InVisit ? VisitRoom : HomeRoom;
        public bool InInterior => Session != null && Session.InPlayerShop&&!Session.IsDedicatedVenue(Session.CurrentPlace);
        /// <summary>在自己的出租屋里（真实的小屋，不是街上弹窗）。</summary>
        public bool InHome => Session != null && Session.CurrentPlace != null && Session.CurrentPlace == Session.Home;
        /// <summary>在任意室内场景（店里或家里）：街道隐藏，镜头跟着老板。</summary>
        private bool VisitingShop => Session?.CurrentPlace is Place p && !Session.InPlayerShop &&
            !Session.IsCityActivityVenue(p) && !Session.IsCityClothingRetail(p) &&
            (GameSession.IsCommercialPremises(p) || p.Business==GameSession.PhoneBusiness || p.Business==Gifts.TobaccoBusiness);
        public bool InRoom => InInterior || InHome || InVisit || VisitingShop || Session?.CurrentPlace?.Model=="office_tower";
        private int RoomKind => VisitingShop ? 6 : InInterior ? 1 : InHome ? 2 : InVisit ? 4 : InPachinkoRoom ? 5 : CityCloseView!=null ? 3 : 0;

        private CameraRig _rig;
        private DayNightLighting _lighting;
        private CityRainView _rain;
        private AvatarView _avatar;
        private PoliceView _police;
        private CrowdView _crowd;
        private Transform _actors;
        private Transform _policeRoot;
        private Place _pendingEnter;
        private string _sleepDestinationId;
        private bool _wasChasing;
        private int _wasRoomKind;
        private bool _frameOldHomeExit;
        private float _roomOverviewSize;
        private string _crowdPlace, _crowdActivity;
        private bool _autosaveDue, _wasPlayerWorking;
        public int ActiveSaveSlot { get; private set; }
        private string _saveCampaignId;
        private bool _bootstrapped;
        private bool _enteringWorld;
        private bool _eventsOn;
        private bool _gameOverShown;

        private void Start()
        {
            if (TryStartPerformanceBenchmark()) return;
            Bootstrap(true);
        }

        /// <summary>搭好整个游戏。showMenus 为假时不弹开局窗口（自动化测试用）。</summary>
        public void Bootstrap(bool showMenus, ulong? seed = null)
        {
            if (_bootstrapped) return;
            _bootstrapped = true;
            _eventsOn = showMenus;
            // Leave headroom above the 120 FPS acceptance target on high-refresh displays.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 144;

            var json = Resources.Load<TextAsset>("Data/city_layout");
            City = CityLayout.FromData(JsonUtility.FromJson<CityLayoutData>(json.text));

            if (showMenus)
            {
                Ui = new GameUi(this);
                Ui.ShowMainMenu();
                return;
            }

            BuildWorld();
            StartSession(NewSession(seed));
            if (showMenus) Ui.ShowMainMenu();
        }

        private void BuildWorld()
        {
            SetupRendering();
            var world = new StreetWorldBuilder(City, 7);
            world.Build();
            CompleteWorld(world);
        }

        private void CompleteWorld(StreetWorldBuilder world)
        {
            World = world;
            _rig.SetCityBounds(world.CityBounds);
            if(!Cam.TryGetComponent<WheelStreetVisibility>(out var wheels))
                wheels=Cam.gameObject.AddComponent<WheelStreetVisibility>();
            wheels.Bind(Cam,world);
            if(!Cam.TryGetComponent<CityOverviewShadows>(out var survey))
                survey=Cam.gameObject.AddComponent<CityOverviewShadows>();
            survey.World=world;
            var checkpoints = new GameObject("DistrictCheckpoints").AddComponent<DistrictGateView>();
            checkpoints.transform.SetParent(transform, false);
            checkpoints.Initialize(this);
            var search=new GameObject("CommunitySearch").AddComponent<CommunitySearchView>();
            search.transform.SetParent(transform,false);search.Initialize(this);
            _lighting = new DayNightLighting(Cam, FindSun(), FindVolume(), World.NightLights);
            _rain = new GameObject("CityRain").AddComponent<CityRainView>();
            _rain.transform.SetParent(transform, false);
            _rain.Initialize(Cam);

            _actors = new GameObject("Actors").transform;
            _avatar = new AvatarView(_actors);
            Interior = new ShopInteriorView(_actors, City.PlayerShop().Size);
            HomeRoom = new HomeRoomView(_actors);
            Ui ??= new GameUi(this);
        }

        private GameSession NewSession(ulong? seed = null) =>
            new GameSession(City, seed ?? (DefaultSeed + (ulong)DateTime.Now.Ticks % 100000), StartingCash,
                GameTimestamp.From(new GameDate(0), GameSession.WakeUpMinute), true, false, false, startWithCookingTutorial: true);

        private void StartSession(GameSession s, int saveSlot = 0, string campaignId = null)
        {
            ResetCommunityReveal();
            CancelCheckout();
            OpeningCheckoutInstructionFollowed = false;
            OpeningPetIntakeInstructionFollowed = false;
            if (PetFocusActive) ExitPetFocus();
            Ui?.CloseAllWindows();
            ClearPachinkoRoom();
            if (Session != null)
            {
                Session.Notice -= OnNotice;
                Session.Failed -= OnFailed;
                Session.Popup -= OnPopup;
                Session.EventCgPopup -= OnEventCgPopup;
                Session.EventCgNotice -= OnEventCgNotice;
                Session.EventRaised -= OnEventRaised;
                Session.ShopExpanded -= OnShopExpanded;
                Session.TripArrived -= OnTripArrived;
                Session.ShopCustomerArrived -= OnCustomerArrived;
                Session.ShopGuestChanged -= OnShopGuestChanged;
                Session.ResidenceSleepFinished -= Ui.OnResidenceSleepFinished;
                Session.TimeAdvanceStarted -= Ui.ShowTimeAdvance;
                Session.Clock.DayAdvanced -= OnDayAdvanced;
                Session.CityTravelRequested-=OnCityTravelRequested;
                Session.CityPlaceEntered-=OnCityPlaceEntered;
                Session.CityActivitySettled-=OnCityActivitySettled;
                Session.CommunityUnlocked -= QueueCommunityReveal;
                Session.Dispose();
            }
            Interior.Clear();
            EnsureInterior(s.PlayerShop.Size, DistrictRoomArt.District(s.City, s.CurrentShop.Place.StreetId));

            Session = s;
            s.DeferRestAdvances = true;
            s.TimeAdvanceStarted += Ui.ShowTimeAdvance;
            Ui.HideTimeAdvance();
            _frameOldHomeExit = City.IsLakeCity && s.CurrentPlace?.Id == "home";
            ActiveSaveSlot = saveSlot;
            _saveCampaignId = campaignId ?? Guid.NewGuid().ToString("N");
            Ui.ResetGuideState();
            s.Notice += OnNotice;
            s.Failed += OnFailed;
            s.Popup += OnPopup;
            s.EventCgPopup += OnEventCgPopup;
            s.EventCgNotice += OnEventCgNotice;
            s.EventRaised += OnEventRaised;
            s.ShopExpanded += OnShopExpanded;
            s.EventsEnabled = !s.IsCityLifeTest && _eventsOn;
            s.TripArrived += OnTripArrived;
            s.ShopCustomerArrived += OnCustomerArrived;
            s.ShopGuestChanged += OnShopGuestChanged;
            s.ResidenceSleepFinished += Ui.OnResidenceSleepFinished;
            s.Clock.DayAdvanced += OnDayAdvanced;
            s.CityTravelRequested+=OnCityTravelRequested;
            s.CityPlaceEntered+=OnCityPlaceEntered;
            s.CityActivitySettled+=OnCityActivitySettled;
            s.CommunityUnlocked += QueueCommunityReveal;
            _cityExitIntent=null;
            _pendingEnter = null;
            string sleepDestination = s.CitySleepRequested ? s.CityIntent?.TargetPlaceId ?? s.CurrentPlace?.Id : null;
            _sleepDestinationId = sleepDestination;
            _gameOverShown = false;
            _autosaveDue = false; _wasPlayerWorking = s.IsWorking;

            if (_policeRoot != null) Kill.It(_policeRoot.gameObject);
            _policeRoot = new GameObject("Police").transform;
            _policeRoot.SetParent(_actors, false);
            _police = new PoliceView(_policeRoot, s.Police);

            if (_crowd == null)
            {
                Vector3 door = World.NodeWorld(s.PlayerShopPlace.NodeId);
                if (World.Places.TryGetValue(s.PlayerShopPlace.Id, out var visual))
                    door = visual.Place.ExplicitEntrance ? visual.Entrance
                        : visual.Entrance + new Vector3(0, 0, StreetWorldBuilder.FrontZAt(visual.Entrance.x) - 0.3f);
                _crowd = new CrowdView(_actors, -95, 100, door);
            }
            SyncCrowdDoor();

            // Apply the initial room visibility, furniture and actor placement before
            // revealing the session. Do not wait for the first gameplay Update.
            SyncViews(0, initialize: true);
            SnapCamera();
            if (s.CurrentPlace != null && !s.IsGameOver && !s.ScooterTutorialPaused) Ui.OnEnteredPlace(s.CurrentPlace);
            if(s.CityIntent!=null&&s.Trip==null&&!s.IsGameOver)
            {
                OnCityTravelRequested(s.CityIntent);
                if (sleepDestination != null && (s.CityIntent?.TargetPlaceId == sleepDestination || s.CurrentPlace?.Id == sleepDestination))
                {
                    _sleepDestinationId = sleepDestination;
                    s.CitySleepRequested = true;
                }
            }
        }

        // ---------- 对 UI 暴露的操作 ----------

        public void NewGame() => EnterGame(() =>
        {
            StartSession(NewSession());
            Ui.ShowOpeningCinematic();
        });

        internal void EnterGame(Action enter)
        {
            if (_enteringWorld) return;
            if (World != null)
            {
                enter();
                return;
            }
            _enteringWorld = true;
            StartCoroutine(EnterFirstSession(enter));
        }

        private IEnumerator BuildWorldIncrementally()
        {
            SetupRendering();
            var world = new StreetWorldBuilder(City, 7);
            var pending = new Stack<IEnumerator>();
            var buildClock=System.Diagnostics.Stopwatch.StartNew();
            long stageStarted=0;
            string activeStage=null;
            void SetStage(string stage)
            {
                if(activeStage!=null)Debug.Log($"[城市加载] {activeStage}: {buildClock.ElapsedMilliseconds-stageStarted} ms");
                activeStage=stage;stageStarted=buildClock.ElapsedMilliseconds;
                Ui.SetWorldLoadingStage(stage);
            }
            pending.Push(world.BuildIncrementally(SetStage,Ui.SetWorldLoadingProgress));
            // Keep a camera rendering behind the overlay so the Editor does not show
            // "No cameras rendering". An empty culling mask still skips the city and
            // its shadow casters throughout the incremental build.
            var loadingCamera=Cam;
            bool cameraWasEnabled=loadingCamera.enabled;
            int previousCullingMask=loadingCamera.cullingMask;
            var previousClearFlags=loadingCamera.clearFlags;
            loadingCamera.cullingMask=0;
            loadingCamera.clearFlags=CameraClearFlags.SolidColor;
            loadingCamera.enabled=true;
            try
            {
                while (pending.Count > 0)
                {
                    var step = pending.Peek();
                    if (!step.MoveNext())
                    {
                        (step as IDisposable)?.Dispose();
                        pending.Pop();
                        continue;
                    }
                    if (step.Current is IEnumerator nested) pending.Push(nested);
                    else yield return step.Current;
                }
                Ui.SetWorldLoadingProgress(.97f);
                SetStage("loading_people");
                yield return null;
                var people = NpcArtCatalog.Prewarm();
                try { while (people.MoveNext()) yield return people.Current; }
                finally { (people as IDisposable)?.Dispose(); }
                var sounds = SoundFx.Prewarm();
                try { while (sounds.MoveNext()) yield return sounds.Current; }
                finally { (sounds as IDisposable)?.Dispose(); }
                CompleteWorld(world);
                Ui.SetWorldLoadingProgress(.99f);
                SetStage("loading_session");
                yield return null;
            }
            finally
            {
                if(loadingCamera!=null)
                {
                    loadingCamera.cullingMask=previousCullingMask;
                    loadingCamera.clearFlags=previousClearFlags;
                    loadingCamera.enabled=cameraWasEnabled;
                }
                if(activeStage!=null)Debug.Log($"[城市加载] {activeStage}: {buildClock.ElapsedMilliseconds-stageStarted} ms");
                Debug.Log($"[城市加载] 总耗时: {buildClock.ElapsedMilliseconds} ms");
                foreach (var step in pending) (step as IDisposable)?.Dispose();
            }
        }

        private IEnumerator EnterFirstSession(Action enter)
        {
            var loading = Ui.ShowWorldLoading();
            bool previousBackground=Application.runInBackground;
            Application.runInBackground=true;
            try
            {
                yield return null;
                yield return null;
                var routine = BuildWorldIncrementally();
                try
                {
                    while (routine.MoveNext()) yield return routine.Current;
                }
                finally { (routine as IDisposable)?.Dispose(); }
                enter();
                Ui.SetWorldLoadingProgress(1);
                Canvas.ForceUpdateCanvases();
                yield return null;
            }
            finally
            {
                Application.runInBackground=previousBackground;
                loading.Close();
                _enteringWorld = false;
            }
        }

        public bool SaveGame(int slot = 0)
        {
            if (!Session.CanSave) return false;
            if (Session.IsCityLifeTest)
            {
                if (!SaveStore.TrySave(Session)) return false;
            }
            else
            {
                if (slot == 0 || !SaveStore.TrySave(Session, slot, campaignId: _saveCampaignId)) return false;
                ActiveSaveSlot = slot;
            }
            SoundFx.Play(Sfx.Success);
            return true;
        }

        public bool TryLoadGame(int slot, bool automatic)
        {
            GameSession s = SaveStore.LoadSlot(City, slot, automatic, out string campaignId);
            if (s == null) return false;
            StartSession(s, automatic ? 0 : slot, campaignId);
            Ui.Toast(MenuLocalization.Get(SaveStore.LastLoadMessage.Contains("备用") ? "loaded_backup" : "loaded"));
            return true;
        }

        private void SaveAutomatically()
        {
            if (Session.IsCityLifeTest)
            {
                Ui.Toast(SaveStore.TrySave(Session, true) ? "已自动存档。" : SaveStore.LastSaveMessage);
                return;
            }
            if (SaveStore.TrySave(Session, SaveStore.AutomaticSlot, automatic: true, campaignId: _saveCampaignId))
            {
                Ui.Toast(MenuLocalization.Get("autosaved"));
            }
            else Ui.Toast(SaveStore.LastSaveMessage);
        }

        /// <summary>前往某地并在到达后进入。已经在门口就直接进去。</summary>
        public void GoToPlace(Place p)
        {
            if (p == null || Session.IsGameOver || Session.IsWorking || Ui.AnyModalOpen) return;
            CancelHomeBedTrip();
            if (Session.CurrentPlace == null && Session.Trip == null && Session.NearbyPlace == p && ShowClosed(p)) return;
            Session.BeginCityTravel(p.Id);
        }

        public void GoHomeToSleep() => GoHomeToSleep(Session.Home);

        public void GoHomeToSleep(Place residence)
        {
            if (Session.IsGameOver || Session.IsWorking || Ui.AnyModalOpen || Placing) return;
            if (!Session.CanSleepAt(residence)) { Ui.Toast("这套住宅目前不能入住。"); return; }
            if (!Session.BeginCityTravel(residence.Id)) return;
            if (Session.CurrentPlace == residence || Session.CityIntent?.TargetPlaceId == residence.Id)
            {
                _sleepDestinationId = residence.Id;
                Session.CitySleepRequested = true;
            }
        }

        private void CancelHomeBedTrip()
        {
            _sleepDestinationId = null;
            if(Session!=null)Session.CitySleepRequested=false;
            HomeRoom?.CancelBedWalk();
            VisitRoom?.CancelBedWalk();
        }

        // Indoor character speeds are authored at 1x; apply the selected speed exactly once.
        private float IndoorWalkDelta(float realDt) => Session.Time.IsRunning
            ? Mathf.Min(realDt,(float)TimeController.MaxTickSeconds)*Session.Time.Speed*(float)CityTravelTiming.PlayerWalkSpeedFactor
            : 0f;

        private void UpdateHomeBedTrip(float dt)
        {
            if (_sleepDestinationId == null) return;
            if (Session.IsGameOver || Session.IsWorking || Placing)
            { CancelHomeBedTrip(); return; }
            if (Session.CurrentPlace?.Id != _sleepDestinationId)
            {
                if (Session.CityIntent?.TargetPlaceId != _sleepDestinationId) CancelHomeBedTrip();
                return;
            }
            if (!Session.CanSleepAt(Session.CurrentPlace)) { CancelHomeBedTrip(); return; }
            if (Ui.AnyModalOpen || !Session.Time.IsRunning) return;
            int result = ActiveResidence.WalkToBed(IndoorWalkDelta(dt));
            if (result == 0) return;
            CancelHomeBedTrip();
            if (result > 0) Ui.OpenBedWindow();
            else Ui.Toast("床边的路被家具挡住了，请先留出通道。");
        }

        internal HomeRoomView BeginResidenceWakeUp()
        {
            if (!Session.CanSleepAt(Session.CurrentPlace)) return null;
            var room = ActiveResidence;
            return room?.TryBeginWakeUp() == true ? room : null;
        }

        private const string CheckoutPathBlocked = "收银台周围没有可走的通道，请调整家具位置。";
        private enum CounterJob { Checkout, PetIntake, PetSettlement }
        private (OwnedShop Shop, CounterJob Job, long Id)? _counterRequest;
        internal bool OpeningCheckoutInstructionFollowed { get; private set; }
        internal bool OpeningPetIntakeInstructionFollowed { get; private set; }
        internal bool CheckoutPending => _counterRequest.HasValue;
        internal bool CanRequestCheckout => InInterior && Interior?.HasCounter == true && !CheckoutPending
            && !Placing && !PetFocusActive && Ui?.AnyModalOpen != true && !Session.IsGameOver && !Session.IsWorking
            && !Session.IsRenovating && !Session.CityActivityExclusive;

        /// <summary>给指定顾客结账。远离柜台时先安排走到可操作的位置。</summary>
        public void Checkout(int customerId) => RequestCounterJob(Session.CurrentShop, CounterJob.Checkout, customerId);

        internal void HandlePetCounterOrder(OwnedShop shop, long id, bool dropOff) =>
            RequestCounterJob(shop, dropOff ? CounterJob.PetIntake : CounterJob.PetSettlement, id);

        private void RequestCounterJob(OwnedShop shop, CounterJob job, long id)
        {
            if (!CanRequestCheckout || Session.CurrentShop != shop) return;
            if (CounterJobBlocker(shop, job, id) is string blocker)
            {
                if (job != CounterJob.Checkout) Ui.Toast(blocker);
                return;
            }
            if (Session.OpeningCounterTutorialPaused)
            {
                if (job == CounterJob.Checkout) OpeningCheckoutInstructionFollowed = true;
                if (job == CounterJob.PetIntake) OpeningPetIntakeInstructionFollowed = true;
                Session.FocusOpeningCounterTutorial(false);
            }
            if (!Interior.NearCounter && !Interior.PlanCheckoutWalk())
            {
                Ui.Toast(CheckoutPathBlocked);
                return;
            }
            Session.CancelCityTravel();
            _cityExitIntent = null;
            Interior.CancelCityDoorWalk();
            CancelHomeBedTrip();
            Session.CancelPlayerRestaurantWork();
            if (Interior.NearCounter)
            {
                CompleteCounterJob(job, id);
                return;
            }
            _counterRequest = (shop, job, id);
            Session.Time.SetPaused(false);
            if (job != CounterJob.Checkout) Ui.Toast("正在前往收银台，WASD / 方向键可接管。");
        }

        private string CounterJobBlocker(OwnedShop shop, CounterJob job, long id) => job switch
        {
            CounterJob.Checkout => shop.Sim.Queue.Any(customer => customer.Id == id) ? null : "这位顾客已不在结账队列中。",
            CounterJob.PetIntake => Session.PetBoarding(shop)?.Offer?.Id == id
                ? Session.PetAcceptBlocker(shop) : "这笔送养订单已不在柜台。",
            _ => Session.PetVisitBlocker(shop, Session.PetGuests(shop).FirstOrDefault(pet => pet.Id == id))
        };

        private void CompleteCounterJob(CounterJob job, long id)
        {
            bool completed = job switch
            {
                CounterJob.Checkout => Session.Checkout((int)id),
                CounterJob.PetIntake => Session.AcceptPetStay(),
                _ => Session.SettlePetStay(id)
            };
            if (completed) Ui.RebuildDock();
        }

        private void UpdateCheckout(float dt)
        {
            if (_counterRequest is not { } request) return;
            if (!InInterior || Session.CurrentShop != request.Shop || Session.IsGameOver || Session.IsWorking || Placing || Session.IsRenovating)
            {
                CancelCheckout();
                return;
            }
            if (CounterJobBlocker(request.Shop, request.Job, request.Id) is string blocker)
            {
                CancelCheckout();
                Ui.Toast(blocker);
                return;
            }
            if (!Session.Time.IsRunning || Ui.AnyModalOpen || Session.RequiredTutorialPaused || PetFocusActive || Session.CityActivityExclusive)
                return;

            int arrived = Interior.WalkToCheckoutCounter(IndoorWalkDelta(dt));
            if (arrived == 0) return;
            CancelCheckout();
            if (arrived < 0) Ui.Toast(CheckoutPathBlocked);
            else CompleteCounterJob(request.Job, request.Id);
        }

        private void CancelCheckout()
        {
            _counterRequest = null;
            Interior?.CancelCheckoutWalk();
        }

        private void CounterInteract()
        {
            if (Session.PlayerShop.Queue.Count > 0) Checkout(Session.PlayerShop.Queue[0].Id);
            else if (Session.CurrentShop.RetailKind == "restaurant") Ui.OpenSpecialBusinessPanel();
            else if (Session.IsPetShop(Session.CurrentShop)) Ui.OpenPetOrders();
            else Ui.Toast("现在没人排队。顾客挑好货会拿到收银台前排队，你站在这里按 F 交货收钱。");
        }

        private DiningOrder NearbyRestaurantTableToClear()
        {
            if (Interior.NearbyDiningTable < 0 || Session.CurrentShop.RetailKind != "restaurant") return null;
            return Session.SpecialBusiness(Session.CurrentShop.Place.Id).Orders.FirstOrDefault(order =>
                order.Table == Interior.NearbyDiningTable && order.NeedsClearing && order.Stage != DiningStage.Burnt);
        }

        private void EnterPlace(Place p)
        {
            _pendingEnter = null;
            if (ShowClosed(p)) return;
            if (Session.Enter(p)) Ui.OnEnteredPlace(p);
        }

        /// <summary>别人的店打烊了：弹窗说清楚（不只是角落里一行字），人留在门口。</summary>
        private bool ShowClosed(Place p)
        {
            string closed = Session.ClosedReason(p);
            if (closed == null) return false;
            SoundFx.Play(Sfx.Error);
            if (RaceTracks.Get(p.Id) != null && closed == Session.RacingAccessBlocker)
                Ui.Popup("赛车场暂未开放", closed);
            else if (Session.RacingGarageEntryBlocker(p.Id) != null)
                Ui.Popup("赛车登记条件未达成", closed);
            else Ui.Popup("打烊了", closed + "\n营业时间再来吧。");
            return true;
        }

        // ---------- 每帧 ----------

        private void Update()
        {
            if (!_bootstrapped) return;
            if(_enteringWorld)
            {
                Ui?.RefreshWorldLoading();
                return;
            }
            if (Session == null)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) Ui?.CloseTopWindow();
                return;
            }
            if (Ui.HandleOpeningCinematicInput()) return;
            float dt = Time.unscaledDeltaTime;
            if (Session.IsTimeAdvancing)
            {
                using (SimulationMarker.Auto()) Session.PumpTimeAdvance(2.0);
                Ui.RefreshTimeAdvance();
                if (!Session.IsTimeAdvancing)
                {
                    using (ViewsMarker.Auto()) SyncViews(0);
                    using (UiMarker.Auto()) Ui.Update(0);
                }
                return;
            }
            if(UpdateRacing(dt))return;
            if (UpdateCommunityReveal(dt)) return;
            HandleKeys();
            if (Session.IsTimeAdvancing) return;
            HandleMovement(dt);
            if (Session.IsTimeAdvancing) return;
            UpdatePlacement();
            if (!Ui.AnyModalOpen && !Placing && !PetFocusActive && !Session.OpeningAgencyGuidePending && !Session.RequiredTutorialPaused) HandleClicks();
            if (Session.IsTimeAdvancing) return;

            using (SimulationMarker.Auto()) Session.Tick(dt);
            if (Session.IsTimeAdvancing) return;
            if (_wasPlayerWorking != Session.IsWorking) { _wasPlayerWorking = Session.IsWorking; _autosaveDue = true; }
            if (_autosaveDue && Session.CanSave && !Session.IsGameOver)
            {
                _autosaveDue = false;
                SaveAutomatically();
            }
            if (Session.IsGameOver && !_gameOverShown)
            {
                _gameOverShown = true;
                Ui.CloseDock();
                Ui.ShowGameOver();
            }
            using (ViewsMarker.Auto()) SyncViews(dt);
            using (UiMarker.Auto()) Ui.Update(dt);
        }

        /// <summary>不依赖 Update 的推进（自动化测试用）。</summary>
        public void Step(float dt)
        {
            if (Session.IsTimeAdvancing)
            {
                using (PCShopSell.Core.Common.RuntimePerformanceProbe.Measure("Frame.Simulation"))
                using (SimulationMarker.Auto()) Session.PumpTimeAdvance(2.0);
                Ui.RefreshTimeAdvance();
                if (!Session.IsTimeAdvancing)
                {
                    using (ViewsMarker.Auto()) SyncViews(0);
                    using (UiMarker.Auto()) Ui.Update(0);
                }
                return;
            }
            UpdatePlacement();
            using (PCShopSell.Core.Common.RuntimePerformanceProbe.Measure("Frame.Simulation"))
            using (SimulationMarker.Auto()) Session.Tick(dt);
            if (Session.IsTimeAdvancing) return;
            using (PCShopSell.Core.Common.RuntimePerformanceProbe.Measure("Frame.Views"))
            using (ViewsMarker.Auto()) SyncViews(dt);
            using (PCShopSell.Core.Common.RuntimePerformanceProbe.Measure("Frame.UI"))
            using (UiMarker.Auto()) Ui.Update(dt);
        }

        private readonly List<ShopBeacon> _beacons = new List<ShopBeacon>();
        private readonly List<ShopBeacon> _ownedResidenceBeacons = new List<ShopBeacon>();
        private ShopBeacon _rentedHomeBeacon;
        public bool ShopBeaconVisible => _beacons.Exists(b => b.Visible);

        /// <summary>用当前会话的门店和产权同步街景外罩。</summary>
        private void UpdateBeacons(float dt)
        {
            var home = Session.Home;
            bool ownsHome = Session.OwnsResidence(home);
            if (!ownsHome)
                _rentedHomeBeacon ??= new ShopBeacon(World.Root.transform, ShopBeacon.Style.RentedResidence);
            _rentedHomeBeacon?.Update(Session, World, ownsHome ? null : home.Id, dt);

            int residenceCount = 0;
            if (ownsHome) MarkOwnedResidence(home.Id);
            var properties = Session.OwnedProperties;
            for (int i = 0; i < properties.Count; i++)
            {
                string id = properties[i].PlaceId;
                if (id != home.Id && World.Places.TryGetValue(id, out var building) && building.Place.Kind == PlaceKind.Home)
                    MarkOwnedResidence(id);
            }
            for (int i = residenceCount; i < _ownedResidenceBeacons.Count; i++)
                _ownedResidenceBeacons[i].Update(Session, World, null, dt);

            var shops = Session.MyShops;
            while (_beacons.Count < shops.Count) _beacons.Add(new ShopBeacon(World.Root.transform));
            for (int i = 0; i < _beacons.Count; i++)
                _beacons[i].Update(Session, World, i < shops.Count ? shops[i].Place.Id : null, dt);

            void MarkOwnedResidence(string id)
            {
                if (_ownedResidenceBeacons.Count == residenceCount)
                    _ownedResidenceBeacons.Add(new ShopBeacon(World.Root.transform, ShopBeacon.Style.OwnedResidence));
                _ownedResidenceBeacons[residenceCount++].Update(Session, World, id, dt);
            }
        }

        private float _crowdSyncTimer;
        private float _shopFrontTimer;
        private float _worldVisualTime;
        private static readonly int WorldVisualTimeId = Shader.PropertyToID("_PCShopWorldVisualTime");

        /// <summary>街坊上班的店门口（站在门前人行道上，进去就消失）。</summary>
        private Vector3? ResidentDoor(string placeId)
        {
            if (!Session.City.HasPlace(placeId)) return null;
            Place p = Session.City.GetPlace(placeId);
            return StreetWorldBuilder.ToWorld(Session.DoorOf(p)) - new Vector3(0, 0, 0.4f);
        }

        private void SyncViews(float dt, bool initialize = false)
        {
            bool scooterTutorialPaused = Session.ScooterTutorialPaused;
            // Keep the rendered pose and camera fixed while the tutorial canvas keeps updating.
            if (scooterTutorialPaused && !initialize)
            {
                _rain.SetPaused(true);
                return;
            }
            if (!scooterTutorialPaused && !_communityRevealActive) _worldVisualTime += dt;
            Shader.SetGlobalFloat(WorldVisualTimeId, _worldVisualTime);
            bool interior = InInterior;
            bool room = InRoom;
            bool worldRunning = !scooterTutorialPaused && Session.Time.IsRunning && !Session.ManualActivityClockPaused;
            float worldDt = worldRunning ? Mathf.Min(dt,(float)TimeController.MaxTickSeconds) * Session.Time.Speed * (float)TimeController.WorldRate : 0f;
            if (interior) EnsureActiveShopRoom();
            if (VisitingShop) EnsureInterior(Session.CurrentPlace.Size,DistrictRoomArt.District(City,Session.CurrentPlace.StreetId));
            Interior.SetActive(interior || VisitingShop);
            if (VisitingShop)
            {
                var p=Session.CurrentPlace;
                string goods=p.Business==Gifts.TobaccoBusiness?"smoke":p.Business==GameSession.PhoneBusiness?"storage":"gpu";
                Interior.Visit(p.Id,Session.IsVacant(p),goods,worldDt,Session.Clock.MinuteOfDay);
            }
            bool residenceChanged = EnsureResidenceView();
            HomeRoom.SetActive(InHome);
            VisitRoom?.SetActive(InVisit);
            World.Root.SetActive(!room);
            World.SetVenueFocus(Session.CurrentPlace?.Id);
            var pachinkoRoom = SyncPachinkoRoom();
            var officePosition = SyncOffice();
            _avatar.Update(Session, dt, World, officePosition ?? pachinkoRoom?.OwnerWorld, officePosition.HasValue ? _officeFacing : pachinkoRoom?.OwnerFacing ?? Vector3.zero);
            SyncExpoView(dt);
            SyncShopEventViews();
            SyncLifeEventViews();
            SyncNeighborhoodViews();
            _police.Update(!room);
            _crowdSyncTimer -= dt;
            string crowdPlace=Session.CurrentPlace?.Id, crowdActivity=Session.RunningCityActivity?.Id;
            if (!room && (_crowdSyncTimer <= 0 || crowdPlace!=_crowdPlace || crowdActivity!=_crowdActivity))
            {
                _crowdPlace=crowdPlace;_crowdActivity=crowdActivity;
                _crowdSyncTimer = 0.25f;
                _crowd.SyncCity(Session, _communityRevealActive ? _rig.Focus : FocusPoint(), Cam, World);
            }
            _crowd.Update(worldDt, !room, pachinkoRoom?.OwnerWorld);
            UpdateBeacons(dt);
            _shopFrontTimer -= dt;
            if (!room && (_shopFrontTimer <= 0 || _wasRoomKind != RoomKind))
            {
                _shopFrontTimer = .25f;
                foreach (PlaceVisual pv in World.Places.Values)
                    if (pv.ClosedOnly.Count > 0) World.SetOpen(pv, Session.IsPlaceOpen(pv.Place));
            }
            if (interior)
            {
                Interior.SetCeilingMeshesHidden(Placing || PetFocusActive);
                Interior.Update(Session, worldDt, dt);
            }
            UpdatePetFocus();
            if (InHome) HomeRoom.Update(Session, dt, Session.Clock.MinuteOfDay);
            if (InVisit) VisitRoom.UpdateVisit(Session);
            UpdateCheckout(dt);
            UpdateHomeBedTrip(dt);
            UpdateCityTravel(dt);
            Vector3? paintedStreet = null;
            if (!room && Session.CurrentPlace==null && City.IsLakeCity && World.Places.ContainsKey("home"))
                paintedStreet = World.ClosestOldQuarterPoint(World.StreetPosition(Session.PlayerPosition));
            FigureMotion.AdvanceAll(dt, worldRunning, paintedStreet);

            if (!_communityRevealActive && (RoomKind != _wasRoomKind || residenceChanged))
            {
                _wasRoomKind = RoomKind;
                SnapCamera();
            }
            else if (!_communityRevealActive)
            {
                _rig.Follow(FocusPoint(), dt);
            }
            bool insideVenue = Session.CurrentPlace != null
                && World.Places.TryGetValue(Session.CurrentPlace.Id, out var venue)
                && venue.VenueInterior != null && venue.VenueInterior.activeInHierarchy;
            UpdateWeather(room || InPachinkoRoom, room || insideVenue);
            _rain.SetPaused(scooterTutorialPaused || _communityRevealActive);

            bool chasing = Session.Police.AnyChasing;
            if (chasing && !_wasChasing) Ui.Toast("被交警发现了！");
            _wasChasing = chasing;
        }

        private void UpdateWeather(bool room, bool sheltered)
        {
            bool raining = Session.CityIsRaining && !sheltered;
            _rain.Sync(raining);
            float paintedDaylight = 0, heading = 0;
            Vector3 quarterCenter = Vector3.zero;
            if (!room && City.IsLakeCity && World.Places.TryGetValue("home",out var oldHome))
            {
                var player = Session.PlayerPosition;
                quarterCenter = World.ClosestOldQuarterPoint(new Vector3((float)player.X,0,(float)player.Y));
                var distance = Vector2.Distance(new Vector2((float)player.X,(float)player.Y),
                    new Vector2(quarterCenter.x,quarterCenter.z));
                paintedDaylight = Mathf.SmoothStep(1,0,Mathf.InverseLerp(28,52,distance));
                heading = oldHome.Root.transform.eulerAngles.y;
            }
            _lighting.Apply(Session.Clock.MinuteOfDay, room || _office!=null, raining, paintedDaylight, heading, quarterCenter,
                !room && City.IsLakeCity ? World.OuterDistrictWeight(new Vector2((float)Session.PlayerPosition.X,(float)Session.PlayerPosition.Y)) : 0,
                _office?.Root.transform.eulerAngles.y ?? 0f,
                (InHome || InVisit) ? ActiveResidence?.District==0 : (InInterior || VisitingShop) && Interior.District==0);
        }

        private bool _overview;

        /// <summary>冒烟 / 调试：店里镜头拉到看全整间店（开 / 关）。</summary>
        public void DebugShopOverview(bool on)
        {
            _overview = on;
            if (!InInterior) return;
            float size = on ? Interior.CameraSize : Mathf.Min(ShopInteriorView.DefaultZoom, Interior.CameraSize);
            _rig.Snap(FocusPoint(size), size);
        }

        private Vector3 FocusPoint(float zoom = -1f)
        {
            if (PetFocusActive && InInterior) return Interior.PetHouseWorld;
            if (Placing && _rig.Edit) return EditFocus(zoom > 0 ? zoom : _rig.Size);
            float size = zoom > 0 ? zoom : _rig.Size;
            float overview = Mathf.InverseLerp(_rig.MinSize, _roomOverviewSize, size);
            if (InHome || InVisit)
            {
                var residence = ActiveResidence ?? HomeRoom;
                return Vector3.Lerp(residence.OwnerWorld + Vector3.up*.8f, residence.Center + Vector3.up*1.5f, overview);
            }
            if (VisitingShop) return Vector3.Lerp(Interior.OwnerWorld + Vector3.up*.8f,Interior.Center + Vector3.up,overview);
            if (InInterior && _overview) return Interior.ViewCenter + new Vector3(0, 0.8f, 0);
            if (InInterior)
            {
                Vector3 center = Vector3.Lerp(Interior.OwnerWorld + Vector3.up*.8f, Interior.Center + Vector3.up*1.5f, overview);
                return center + CameraRig.Rotation * Vector3.right * (size * Cam.aspect * ShopDockFraction);
            }
              var closeView=CityCloseView;
              if(ExpoView?.MonthlyOpen==true)return ExpoView.MonthlyFocus-CameraRig.Rotation*Vector3.right*(size*Cam.aspect*.16f);
              if(ExpoView?.Open==true)return ExpoCameraFocus(zoom>0?zoom:_rig.Size);
              if(closeView!=null)return CityCloseFocus(closeView, overview);
            if (Session.CurrentPlace?.Id == GameSession.OldMarketVenueId && World.Places.TryGetValue(GameSession.OldMarketVenueId, out PlaceVisual marketView))
                return marketView.Root.transform.TransformPoint(new Vector3(0, 1, (float)Session.CurrentPlace.Depth * .46f));
            Vector3 p = World.StreetPosition(Session.PlayerPosition);
            if (Session.CurrentPlace != null && Session.RunningCityActivity?.DefinitionId != "race_night" && World.Places.TryGetValue(Session.CurrentPlace.Id, out PlaceVisual pv)) p = pv.Entrance;
            return p + new Vector3(0, 1f, 0);
        }

        private float ShopDockFraction => Ui.DockOpen ? Mathf.Min(.48f, GameUi.ShopDockWidth / Mathf.Max(1f, Ui.CanvasWidth)) : 0f;

        private float FrameRoom(float width, float depth, float coveredWidth)
        {
            var right = CameraRig.Rotation * Vector3.right;
            var up = CameraRig.Rotation * Vector3.up;
            float halfWidth = (Mathf.Abs(right.x)*width + Mathf.Abs(right.z)*depth)*.5f;
            float halfHeight = (Mathf.Abs(up.x)*width + Mathf.Abs(up.z)*depth + Mathf.Abs(up.y)*3f)*.5f;
            return Mathf.Max((halfWidth+.6f)/(Cam.aspect*(1f-coveredWidth)), (halfHeight+.6f)/.83f);
        }

        private void HandleKeys()
        {
            if (PetFocusActive) { HandlePetFocusKeys(); return; }
            if (Ui.MainMenuOpen)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) Ui.CloseTopWindow();
                return;
            }
            if (Session.RequiredTutorialPaused)
            {
                _orbiting = false;
                if ((Session.ScooterTutorialPaused || Session.BankTutorialPaused) && !Ui.AnyModalOpen) return;
                if (Input.GetKeyDown(KeyCode.Escape) && (!Ui.AnyModalOpen || !Ui.RequiredTutorialControlVisible)
                    && !Ui.CloseTopWindow()) Ui.TryHotkey(KeyCode.Escape);
                return;
            }
            if (Session.OpeningAgencyGuidePending)
            {
                _orbiting = false;
                if (Input.GetKeyDown(KeyCode.Escape) && !Ui.CloseTopWindow()) Ui.TryHotkey(KeyCode.Escape);
                return;
            }
            var selected = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
            var input = selected != null ? selected.GetComponent<UnityEngine.UI.InputField>() : null;
            if (input != null && input.isFocused) return;
            if (Ui.AmusementInputActive)
            {
                _orbiting = false;
                if (Input.GetKeyDown(KeyCode.Escape)) Ui.CloseTopWindow();
                return;
            }
            if(Ui.ExpoHudOpen)
            {
                if(!GameUi.PointerOverUi)_rig.Zoom(Input.mouseScrollDelta.y);
                HandleCameraOrbit();HandleExpoPointer();
                if(Input.GetKeyDown(KeyCode.V))FocusExpo("overview");
                if(Input.GetKeyDown(KeyCode.F)&&Session.BrandExpoExclusive)FocusExpo(ExpoMoments.ZoneAt(Session.ActiveBrandExpo.Day,Session.ActiveBrandExpo.Decision));
                if(Input.GetKeyDown(KeyCode.Escape))Ui.CloseTopWindow();
                return;
            }
            if (Session.IsWorking)
            {
                HandleTimeSpeedKeys();
                return;
            }
            if (Input.GetKeyDown(KeyCode.Space) && !Ui.FishingInputActive && !Ui.OnsiteInputActive && !Ui.KaraokeInputActive && !Ui.LeisureGameInputActive)
                Session.Time.TogglePause();
            HandleTimeSpeedKeys();
            if (!GameUi.PointerOverUi) _rig.Zoom(Input.mouseScrollDelta.y);
            HandleCameraOrbit();
            // Esc：关最上面一层窗口；没开窗口时打开菜单。右键不关闭界面。
            if (Input.GetKeyDown(KeyCode.Escape) && !Placing && !Ui.CloseTopWindow() && !Ui.AnyModalOpen) Ui.TryHotkey(KeyCode.Escape);
            if (!Placing) Ui.HandleHudPanelKeys();
            if (Input.GetKeyDown(KeyCode.F) && !Ui.AnyModalOpen && !Session.IsGameOver && !Placing)
            {
                if (VisitingShop) { if(Interior.NearDoor) Session.Leave(); else if(Interior.NearCounter) Ui.OnEnteredPlace(Session.CurrentPlace); }
                else if (_office != null) InteractOffice();
                else if (InVisit) InteractVisitRoom();
                else if (InHome) HomeInteract(HomeRoom.NearbyId);
                else if (InPachinkoRoom) InteractPachinkoRoom();
                else if(Session.IsOwnedVenue(Session.CurrentPlace))Ui.OpenVenueManagement(Session.CurrentPlace);
                else if (InInterior)
                {
                    if (Interior.NearbyPetOwner(out _) is PetStay pet) Ui.OpenPetOwnerVisit(pet.Id);
                    else if (Interior.NearModelPaintBench) Ui.OpenModelCommissions();
                    else if (Interior.NearUsedRepairBench) Ui.OpenUsedRepairDesk();
                    else if (Interior.NearUsedBuyDesk) Ui.OpenUsedDesk();
                    else if (NearbyRestaurantTableToClear() is DiningOrder meal)
                    {
                        if (RequestRestaurantWork(meal.Id, RestaurantJob.Clear)) Ui.RebuildDock();
                    }
                    else if (Interior.NearCounter && Session.PlayerShop.Queue.Count > 0) CounterInteract();
                    else if (Interior.NearPetHouse && Session.IsPetShop(Session.CurrentShop)) EnterPetFocus();
                    else if(Session.IsSpecialBusiness(Session.CurrentShop)&&Interior.NearSpecialFurniture)Ui.OpenSpecialBusinessPanel();
                    else if (Interior.NearCounter) CounterInteract();
                    else if (Interior.NearDoor) Ui.ConfirmLeaveShop();
                    else if (Interior.NearFridge) Ui.OpenFridgeWindow();
                    else if (Interior.NearBox) Ui.OpenShopLockerWindow();
                    else ShelfInteract();
                }
                else if(Session.NearbyCommunityBox>=0)Session.SearchCommunityBox(Session.NearbyCommunityBox);
                else TryEnterNearby();
            }
        }

        private void HandleTimeSpeedKeys()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) Session.Time.SetSpeed(1);
            if (Input.GetKeyDown(KeyCode.Alpha2)) Session.Time.SetSpeed(2);
            if (Input.GetKeyDown(KeyCode.Alpha3)) Session.Time.SetSpeed(4);
            if (Input.GetKeyDown(KeyCode.Alpha4)) Session.Time.SetSpeed(8);
        }

        // ---------- 镜头旋转 ----------

        private bool _orbiting;

        /// <summary>街上、家里和店里共用视角操作：右键拖动旋转/俯仰，Q / E 左右转，V 复位。</summary>
        private void HandleCameraOrbit()
        {
            if (Placing)
            {
                // 编辑模式：中键拖动（或手里没拿家具时右键拖动）旋转 / 俯仰，Q / E 左右转，V 复位。右键在手里有家具时是“放回”，不能拿来转。
                bool drag = Input.GetMouseButton(2) || (_placeId == null && Input.GetMouseButton(1));
                bool start = Input.GetMouseButtonDown(2) || (_placeId == null && Input.GetMouseButtonDown(1));
                if (start && !GameUi.PointerOverUi) _orbiting = true;
                if (!drag) _orbiting = false;
                if (_orbiting) _rig.Orbit(Input.GetAxis("Mouse X") * 4f, -Input.GetAxis("Mouse Y") * 3f);
                float ke = (Input.GetKey(KeyCode.E) ? 1f : 0f) - (Input.GetKey(KeyCode.Q) ? 1f : 0f);
                if (Mathf.Abs(ke) > 0.01f) _rig.Orbit(ke * 100f * Time.unscaledDeltaTime, 0f);
                if (Input.GetKeyDown(KeyCode.V)) _rig.ResetView();
                return;
            }
            if (Ui.AnyModalOpen && !Ui.ExpoHudOpen) { _orbiting = false; return; }
            if (Input.GetMouseButtonDown(1) && !GameUi.PointerOverUi) _orbiting = true;
            if (!Input.GetMouseButton(1)) _orbiting = false;
            if (_orbiting) _rig.Orbit(Input.GetAxis("Mouse X") * 4f, -Input.GetAxis("Mouse Y") * 3f);
            float k = (Input.GetKey(KeyCode.E) ? 1f : 0f) - (Input.GetKey(KeyCode.Q) ? 1f : 0f);
            if (Mathf.Abs(k) > 0.01f) _rig.Orbit(k * 100f * Time.unscaledDeltaTime, 0f);
            if (Input.GetKeyDown(KeyCode.V)) _rig.ResetView();
        }

        // ---------- WASD 走路 ----------

        private bool _taxiHinted;

        /// <summary>WASD / 方向键。相对镜头方向走：W 是屏幕往上（镜头怎么转，方向跟着转）。街上在街道范围内自由走，店里在店内走。</summary>
        private void HandleMovement(float dt)
        {
            var direction = DirectionalInput.Held;
            // A held direction must not cancel a pause just triggered with Space.
            bool newMovement = Session.Time.IsUserPaused && !Input.GetKeyDown(KeyCode.Space) && DirectionalInput.AnyPressed;
            ApplyMovement(dt, direction.x, direction.y, newMovement);
        }

        private void ApplyMovement(float dt, float h, float v, bool newMovement)
        {
            if (PetFocusActive || Session.IsWorking || Session.CityActivityExclusive || Ui.AnyModalOpen || Session.IsGameOver
                || Session.OpeningAgencyGuidePending || Session.RequiredTutorialPaused || Session.Time.IsHeld || Placing ||
                (Session.Time.IsUserPaused && !newMovement) || (h == 0 && v == 0))
            {
                Session.SetSteer(0, 0);
                return;
            }

            if (!InRoom && _office == null && !InPachinkoRoom && !Session.CanSteer)
            {
                if (!_taxiHinted)
                {
                    _taxiHinted = true;
                    Ui.Toast("打车由司机开：点下方“去…”按钮。想自己走，换成步行、小电驴或三轮车。");
                }
                Session.SetSteer(0, 0);
                return;
            }

            Session.Time.SetPaused(false);
            CancelCheckout();
            CancelHomeBedTrip();
            Session.CancelCityTravel();_cityExitIntent=null;Interior?.CancelCityDoorWalk();
            Session.CancelPlayerRestaurantWork();
            Vector3 fwd = Cam.transform.forward;
            fwd.y = 0;
            fwd.Normalize();
            Vector3 right = Cam.transform.right;
            right.y = 0;
            right.Normalize();
            Vector3 dir = fwd * v + right * h;

            if (VisitingShop)
            {
                Session.SetSteer(0,0); Interior.MoveOwner(dir, IndoorWalkDelta(dt));
                if(Interior.OwnerOutside) Session.Leave();
                return;
            }
            if (InInterior)
            {
                Session.SetSteer(0, 0);
                Interior.MoveOwner(dir, IndoorWalkDelta(dt));
                if (Interior.OwnerOutside)
                {
                    Interior.StepBackInside();
                    Ui.ConfirmLeaveShop();
                }
                return;
            }
            if (InHome || InVisit)
            {
                Session.SetSteer(0, 0);
                MoveInResidence(dir, dt);
                return;
            }
            if (_office != null)
            {
                Session.SetSteer(0,0);
                WalkOffice(dir,IndoorWalkDelta(dt));
                return;
            }
            if (InPachinkoRoom)
            {
                Session.SetSteer(0, 0);
                SyncPachinkoRoom()?.MoveOwner(dir, IndoorWalkDelta(dt));
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

        /// <summary>出租屋里的互动：床 = 睡觉，衣柜 = 换衣服，电饭锅 = 做饭，凳子 / 沙发 = 坐下歇，门 = 出门。查看房间信息用右上角“我的”。</summary>
        public void MoveInResidence(Vector3 direction, float realDt)
        {
            if (!(InHome || InVisit) || Ui.AnyModalOpen || !Session.Time.IsRunning || Session.IsGameOver) return;
            var room = ActiveResidence;
            if (room == null) return;
            Vector3 before = room.OwnerWorld;
            room.MoveOwner(direction, IndoorWalkDelta(realDt));
            if (InHome) Ui.RecordOpeningWalk(Vector3.Distance(before, room.OwnerWorld));
        }

        private void InteractVisitRoom()
        {
            string target = VisitRoom?.NearbyId;
            if (target == HomeRoomView.Door) LeaveHome();
            else if (target == HomeRoomView.Bed && Session.CanSleepAt(Session.CurrentPlace)) Ui.OpenBedWindow();
            else if (target == HomeRoomView.Storage && Session.HasSecondaryResidenceWarehouse) Ui.OpenStorageWindow();
            else if (target == HomeRoomView.Fridge && Session.CanOpenFridge) Ui.OpenFridgeWindow();
            else if (target != null && Session.CitySamePlace(target) && VisitRoom.CanReachResident(target))
                Ui.OpenCityPerson(target);
            else Ui.Toast("走近住户或房门，再按 F。");
        }

        public void HomeInteract(string id)
        {
            if (Ui.OpeningMealPreludePending || (Session.OpeningMealTutorialPending && id != HomeRoomView.Cooker))
            { Ui.RemindOpeningMeal(); return; }
            CancelHomeBedTrip();
            switch (id)
            {
                case HomeRoomView.Bed:
                    Ui.OpenBedWindow();
                    break;
                case HomeRoomView.Wardrobe:
                    Ui.OpenWardrobeWindow();
                    break;
                case HomeRoomView.Cooker:
                    Ui.OpenCookerWindow();
                    break;
                case HomeRoomView.Repair:
                    Ui.OpenRepairWindow();
                    break;
                case HomeRoomView.Climate:
                    Ui.OpenCityClimate();
                    break;
                case HomeRoomView.Fridge:
                    Ui.OpenFridgeWindow();
                    break;
                case HomeRoomView.Storage:
                    Ui.OpenStorageWindow();
                    break;
                case HomeRoomView.Seat:
                    HomeRoom.ToggleSit();
                    break;
                case HomeRoomView.Door:
                    LeaveHome();
                    break;
                default:
                    Ui.Toast("走到床、衣柜、电饭锅或门边，按 F。");
                    break;
            }
        }

        /// <summary>现在能交互的东西：头顶提示的位置和文字。没有则返回 false。</summary>
        public bool TryGetInteract(out Vector3 world, out string label)
        {
            world = Vector3.zero;
            label = null;
            if (Session == null || Session.IsGameOver || Ui.AnyModalOpen || Placing) return false;
            if (VisitingShop)
            {
                if(Interior.NearDoor){world=Interior.DoorWorld;label="店铺出口（按 F）";return true;}
                if(Interior.NearCounter){world=Interior.CounterWorld;label="柜台（按 F）";return true;}
                return false;
            }
            if (_office != null)
            {
                if (NearOfficeReception)
                {
                    var desk=DistrictOffice.ReceptionCenter((float)_office.Place.Width,(float)_office.Place.Depth);
                    world=_office.Root.transform.TransformPoint(new Vector3(desk.x,1.6f,desk.y));
                    label="写字楼前台";
                    return true;
                }
                world=_office.Entrance+Vector3.up*1.6f; label="大厅出口"; return OfficeDoor;
            }
            if (InPachinkoRoom) return TryGetPachinkoInteract(out world, out label);
            if(Session.IsOwnedVenue(Session.CurrentPlace))
            {world=StreetWorldBuilder.ToWorld(Session.PlayerPosition)+Vector3.up*2;label="场馆管理（按 F 打开）";return true;}
            if (InHome || InVisit)
            {
                var room = ActiveResidence;
                if (room?.NearbyId == null) return false;
                world = room.NearbyWorld;
                label = room.NearbyLabel;
                if (InHome && Session.OpeningMealTutorialPending && room.NearbyId != HomeRoomView.Cooker)
                    label = "先做饭 · 请先使用电饭锅";
                return true;
            }
            if (InInterior)
            {
                if (Session.InPlayerShop && !GameUi.PointerOverUi)
                {
                    FixturePick hover = Interior.PickFixture(Cam.ScreenPointToRay(Input.mousePosition), out _, out string kindId, out Vector3 at);
                    if (hover != FixturePick.None)
                    {
                        world = at;
                        ShelfKind hk = ShelfKinds.Get(kindId);
                        label = hover == FixturePick.Shelf ? (hk == null ? "货架（还没设置摆什么）· 左键设置" : $"货架：{hk.Name} · 左键查看 / 设置")
                            : hover == FixturePick.Display ? "展示台 · 左键查看" : "装机台 · 左键打开";
                        return true;
                    }
                }
                if (Interior.NearbyPetOwner(out world) is PetStay pet)
                {
                    var person = Session.CityPerson(pet.OwnerId);
                    bool dropOff = Session.PetBoarding(Session.CurrentShop)?.Offer?.Id == pet.Id;
                    label = (person?.Name ?? "宠物主人") + (dropOff ? " · 送养" : " · 探望结算") + "（按 F）";
                    return true;
                }
                if (Interior.NearModelPaintBench)
                {
                    world = Interior.ModelPaintBenchWorld; label = "喷漆台 · 按 F 查看模型委托"; return true;
                }
                if (Interior.NearUsedRepairBench)
                {
                    world = Interior.UsedRepairBenchWorld; label = "二手修复台 · 按 F 安排修复／查看进度"; return true;
                }
                if (Interior.NearUsedBuyDesk)
                {
                    world = Interior.UsedBuyDeskWorld; label = "收购鉴定台 · 按 F 办理收购或鉴定"; return true;
                }
                if (NearbyRestaurantTableToClear() is DiningOrder meal)
                {
                    world = Interior.SpecialFurnitureWorld;
                    label = meal.Stage == DiningStage.Checkout ? "收桌并收款（按 F）" : "收桌（按 F）";
                    return true;
                }
                if(Session.IsSpecialBusiness(Session.CurrentShop)&&Interior.NearSpecialFurniture&&!(Interior.NearCounter&&(Session.PlayerShop.Queue.Count>0)))
                {
                    world=Interior.SpecialFurnitureWorld;label=Interior.SpecialFurnitureName+"：经营操作（按 F 打开）";return true;
                }
                if (Interior.NearPetHouse && Session.IsPetShop(Session.CurrentShop) && !(Interior.NearCounter && Session.PlayerShop.Queue.Count > 0))
                {
                    world = Interior.PetHouseWorld + Vector3.up*.5f; label = "宠物屋 · 按 F 拉近镜头"; return true;
                }
                if (Interior.NearCounter)
                {
                    var queue = Session.PlayerShop.Queue;
                    world = Interior.CounterWorld;
                    label = queue.Count > 0
                        ? $"收银：给顾客结账 {queue[0].ItemName} {queue[0].Spend}（{queue.Count} 人排队）"
                        : Session.CurrentShop.RetailKind == "restaurant" ? "餐馆经营（按 F）" : "收银台（现在没人排队）";
                    return true;
                }
                if (Interior.NearDoor)
                {
                    world = Interior.DoorWorld;
                    label = Session.LeaveBlocker ?? $"出门（{Session.ShopLeaveStatus().Message}）";
                    return true;
                }
                if (Interior.NearFridge)
                {
                    world = Interior.NearFridgeWorld; label = "冰箱：存取食物（按 F 打开）"; return true;
                }
                if (Interior.NearBox)
                {
                    world = Interior.NearBoxWorld;
                    label = "纸箱：背包与后仓（容量不限，按 F 打开）";
                    return true;
                }
                if (Interior.NearBench)
                {
                    int n = Session.PlayerShop.Orders.Count;
                    world = Interior.NearBenchWorld;
                    label = n > 0 ? $"装机台（{n} 个装机单）" : "装机台（暂时没有装机单）";
                    return true;
                }
                if (!Interior.NearShelf)
                {
                    if (!Interior.NearDisplay) return false;
                    world = Interior.NearDisplayWorld;
                    label = Session.PlayerShop.Orders.Count > 0 ? $"查看整机展示（{Session.PlayerShop.Orders.Count} 个装机单）" : "查看整机展示";
                    return true;
                }
                world = Interior.NearbyShelfWorld;
                ShelfKind shelfKind = ShelfKinds.Get(Interior.NearbyShelfKind);
                if (shelfKind == null) label = "货架（还没设置摆什么，按 F 设置）";
                else label = $"货架：{shelfKind.Name}（货架上 {Session.PlayerShop.ShelfFillOf(shelfKind).Total} 件，按 F 查看 / 设置）";
                return true;
            }
            if(Session.NearbyCommunityBox>=0)
            {
                int box=Session.NearbyCommunityBox;
                world=StreetWorldBuilder.ToWorld(Session.CommunityBoxPosition(box))+Vector3.up;
                label="调查"+GameSession.CommunityBoxName(box)+"（按 F）";return true;
            }
            if (Session.NearbyPlace is Place residence && residence.Kind == PlaceKind.Home)
            {
                world = StreetWorldBuilder.ToWorld(Session.DoorOf(residence), 3.4f);
                label = Session.CanSleepAt(residence) ? "回家 · " + Session.PlaceTitle(residence) : "查看住户 · " + residence.Name;
                return true;
            }
            if (NearPasserby(out ResidentDef who, out Vector3 pw))
            {
                world = pw + new Vector3(0, 2.2f, 0);
                label = $"和{who.Person.Name}（{who.Person.Role}）聊聊";
                return true;
            }
            Place p = Session.NearbyPlace;
            if (p == null || Session.CurrentPlace != null || !World.Places.TryGetValue(p.Id, out PlaceVisual pv)) return false;
            world = StreetWorldBuilder.ToWorld(Session.DoorOf(p), 3.4f);
            label = "进入 " + Session.PlaceTitle(p);
            return true;
        }

        /// <summary>店里走到货架前按 F：看这个货架上摆的什么、还剩多少。</summary>
        private void ShelfInteract()
        {
            if (Interior.NearBench)
            {
                Ui.OpenBenchWindow();
                return;
            }
            if (!Interior.NearShelf)
            {
                if (Interior.NearDisplay)
                {
                    Ui.OpenDisplayWindow();
                    return;
                }
                Ui.Toast("走到货架或展示台跟前按 F，可以看摆了什么。");
                return;
            }
            Ui.OpenShelfWindow(Interior.NearbyShelf);
        }

        public void LeaveHome()
        {
            CancelHomeBedTrip();
            if (Session.LeaveBlocker != null) { Session.Leave(); return; }
            Ui.CloseAllWindows();
            LeaveCurrentPlace();
        }

        public void LeaveCurrentPlace()
        {
            if (Session.CurrentPlace == null) return;
            CancelCheckout();
            Session.Leave();
            if (Session.CurrentPlace == null) Session.Time.SetPaused(false);
        }

        /// <summary>从店门走出去回到街上；走不开（有事拦着）就退回门里。</summary>
        public void LeaveShop()
        {
            Ui.CloseAllWindows();
            Ui.CloseDock();
            LeaveCurrentPlace();
            if (Session.InPlayerShop)
            {
                Interior.StepBackInside();
                Ui.OpenDock();
            }
        }

        /// <summary>街上身边 1.3 米内的路人（比门口更近时优先和人说话）。</summary>
        private bool NearPasserby(out ResidentDef who, out Vector3 world)
        {
            who = null;
            world = Vector3.zero;
            if (_crowd == null || InRoom || Session.CurrentPlace != null) return false;
            return _crowd.Nearest(World.StreetPosition(Session.PlayerPosition), 1.3f, out who, out world);
        }

        /// <summary>和街上的人说话：他停下来面向你，窗口关掉后接着走。</summary>
        private void TalkToPasserby(ResidentDef who)
        {
            Session.SetSteer(0, 0);
            if (Session.Trip != null) Session.StopTrip();
            _crowd.Talking = who.Id;
            _crowd.TalkFace = StreetWorldBuilder.ToWorld(Session.PlayerPosition);
            Ui.OpenResidentTalk(who, () => { if (_crowd.Talking == who.Id) _crowd.Talking = null; });
        }

        private void TryEnterNearby()
        {
            Place p = Session.NearbyPlace;
            bool atResidence = p?.Kind == PlaceKind.Home;
            if (atResidence && !Session.CanSleepAt(p))
            {
                Session.SetSteer(0, 0);
                Ui.OpenResidence(p);
                return;
            }
            if (!atResidence && NearPasserby(out ResidentDef who, out _))
            {
                TalkToPasserby(who);
                return;
            }
            if (p == null)
            {
                Ui.Toast("附近没有能进的地方，走到门口再按 F。");
                return;
            }
            Session.SetSteer(0, 0);
            if (Session.Trip != null) Session.StopTrip();
            if (ShowClosed(p)) return;
            if (Session.EnterNearby())
            {
                Session.Time.SetPaused(false);
                Ui.OnEnteredPlace(p);
            }
        }

        private void HandleClicks()
        {
            if (Session.IsWorking || InPachinkoRoom) return;
            if (!Input.GetMouseButtonDown(0) || GameUi.PointerOverUi) return;
            CancelHomeBedTrip();
            Ray ray = Cam.ScreenPointToRay(Input.mousePosition);
            var hits = Physics.RaycastAll(ray, 1000f);
            foreach(var personHit in hits)
            {
                var person=personHit.collider.GetComponentInParent<CityPersonMarker>();
                if(person==null)continue;
                var customer=personHit.collider.GetComponentInParent<CustomerMarker>();
                if(customer!=null&&!Input.GetKey(KeyCode.LeftShift)&&!Input.GetKey(KeyCode.RightShift))continue;
                if(Session.CitySamePlace(person.PersonId))Ui.OpenCityPerson(person.PersonId);
                else Ui.Toast("走近这位居民后再交谈；地图不会把对方召唤过来。");
                return;
            }
            foreach (var passerbyHit in hits)
            {
                var passerby = passerbyHit.collider.GetComponentInParent<AmbientPersonMarker>();
                if (passerby == null) continue;
                Ui.Toast("普通行人 · " + passerby.Status);
                return;
            }
            if (InInterior)
            {
                // 货架、柜台的碰撞体也在场景里：从所有命中里挑顾客
                foreach (RaycastHit h in Physics.RaycastAll(ray, 1000f))
                {
                    var cm = h.collider.GetComponentInParent<CustomerMarker>();
                    if (cm != null)
                    {
                        var shopper = Session.PlayerShop.ActiveCustomers.FirstOrDefault(customer => customer.Id == cm.CustomerId);
                        if (shopper != null && shopper.State == PCShopSell.Core.Shop.CustomerState.Browsing)
                        {
                            Ui.Toast(string.IsNullOrEmpty(shopper.PersonId) ? "普通顾客 · 正在挑选商品" : "正在挑选商品；点击头顶姓名可以交谈。");
                            return;
                        }
                        Checkout(cm.CustomerId);
                        return;
                    }
                }
                if (Ui.AnyModalOpen || Placing || !Session.InPlayerShop) return;
                foreach(var deviceHit in Physics.RaycastAll(ray,1000f))
                {
                    var pv=deviceHit.collider.GetComponentInParent<PlacedVisual>();
                    if(pv==null||!CityWeather.IsDevice(pv.FurnitureId)&&pv.FurnitureId!=PCShopSell.Core.Home.FurnitureCatalog.FittingMirror)continue;
                    Vector3 offset=pv.transform.position-Interior.OwnerWorld;offset.y=0;
                    if(offset.magnitude<=3){if(pv.FurnitureId==PCShopSell.Core.Home.FurnitureCatalog.FittingMirror)Ui.OpenCityWardrobe();else Ui.OpenCityClimate();}else Ui.Toast("走近设备后再操作。");
                    return;
                }
                switch (Interior.PickFixture(ray, out int shelf, out _, out _))
                {
                    case FixturePick.Shelf: Ui.OpenShelfWindow(shelf); break;
                    case FixturePick.Display: Ui.OpenDisplayWindow(); break;
                    case FixturePick.Bench: Ui.OpenBenchWindow(); break;
                }
                return;
            }
            if (!Physics.Raycast(ray, out RaycastHit hit, 1000f)) return;

            if (InHome)
            {
                string clicked = null;
                var hf = hit.collider.GetComponentInParent<HomeFurniture>();
                if (hf != null) clicked = hf.Id;
                var pv = hit.collider.GetComponentInParent<PlacedVisual>();
                if (pv != null)
                {
                    FurnitureDef fd = FurnitureCatalog.Get(pv.FurnitureId);
                    clicked = fd == null ? null : HomeRoomView.RoleId(fd.Role);
                }
                if (clicked != null)
                {
                    if (HomeRoom.NearbyId == clicked) HomeInteract(clicked);
                    else Ui.Toast("再走近一点，按 F。");
                }
                return;
            }

            // 整个游戏都不点击移动：WASD 走路，F 交互。
        }

        // ---------- 会话事件 ----------

        private void OnNotice(string m) => Ui.Toast(m);
        private void OnEventCgNotice(string message, EventCgFrame cg) =>
            Ui.Toast(message, Session.IsWorking || Session.BrandExpoExclusive ? null : cg);

        private void OnFailed(string m) => SoundFx.Play(Sfx.Error);

        private void OnEventRaised(EventDef def)
        {
            if (_communityRevealPending || _communityRevealActive) return;
            SoundFx.Play(Sfx.Notification);
            Ui.ShowEvent(def);
        }

        /// <summary>店内场景按店面大小重建（扩建、读档时用）。</summary>
        private void EnsureInterior(PCShopSell.Core.Shop.ShopSize size, int district, bool rebuild = false)
        {
            if (!rebuild && Interior != null && Interior.Size == size && Interior.District == district) return;
            Interior?.Destroy();
            Interior = new ShopInteriorView(_actors, size, district);
        }

        private void EnsureActiveShopRoom() => EnsureInterior(Session.PlayerShop.Size,
            DistrictRoomArt.District(City, Session.CurrentShop.Place.StreetId));

        private bool EnsureResidenceView()
        {
            if (!InHome && !InVisit) return false;
            var room = InHome ? Session.ResidenceRoom : Session.RoomForResidence(Session.CurrentPlace);
            var view = ActiveResidence;
            int district = DistrictRoomArt.District(City, Session.CurrentPlace?.StreetId);
            if (view?.Room == room && view.District == district) return false;
            view?.Destroy();
            view = new HomeRoomView(_actors, room, district);
            if (InHome) HomeRoom = view;
            else VisitRoom = view;
            return true;
        }

        /// <summary>签了新铺子（搬店）：店内场景按新铺面重建，街上顾客改往新店门走。</summary>
        private void OnShopExpanded(PCShopSell.Core.Shop.ShopSize size)
        {
            EnsureInterior(size, DistrictRoomArt.District(City, Session.CurrentShop.Place.StreetId), true);
            SyncCrowdDoor();
            SnapCamera();
        }

        private void SyncCrowdDoor()
        {
            if (_crowd != null && World.Places.TryGetValue(Session.PlayerShopPlace.Id, out PlaceVisual pv))
                _crowd.ShopDoor = (pv.Place.ExplicitEntrance ? pv.Entrance : pv.Entrance + new Vector3(0, 0, StreetWorldBuilder.FrontZAt(pv.Entrance.x) - 0.3f));
        }

        /// <summary>进入室内时取全景，拉近后跟随玩家。</summary>
        public void SnapCamera(bool smooth = false)
        {
            bool interior = InInterior || VisitingShop;
            bool home = InHome || InVisit;
            if (home) EnsureResidenceView();
            if (InInterior) EnsureActiveShopRoom();
            else if (VisitingShop) EnsureInterior(Session.CurrentPlace.Size,DistrictRoomArt.District(City,Session.CurrentPlace.StreetId));
            var closeView=interior||home?null:CityCloseView;
            bool editing = Placing && _rig.Edit;
            if (editing) _rig.SetEdit(false);
            _rig.SetInterior(interior || home || closeView!=null);
            if(closeView!=null)_rig.SetPlaceAngles(closeView.Root.transform.eulerAngles.y + (closeView.Place.Model == "office_tower" ? 34f : InPachinkoRoom ? 18f : 0f),
                closeView.Place.Model == "office_tower" ? 34f : InPachinkoRoom ? 49f : 61f);
            else if(interior||home)_rig.SetPlaceAngles(CameraRig.DefaultYaw,CameraRig.DefaultPitch);
            _rig.MinSize = home ? HomeRoomView.MinZoom : interior ? ShopInteriorView.MinZoom : 5f;
            float size = closeView!=null?9.5f:16f;
            if (_frameOldHomeExit && Session.CurrentPlace==null)
            {
                _frameOldHomeExit = false;
                var player = World.StreetPosition(Session.PlayerPosition);
                if (World.Places.TryGetValue("home",out var oldHome) && (player-oldHome.Entrance).sqrMagnitude<36f)
                {
                    _rig.Orbit(Mathf.DeltaAngle(CameraRig.Yaw,oldHome.Root.transform.eulerAngles.y+52.6f),
                        33.5f-CameraRig.Pitch);
                    size = Mathf.Max(9.33f,16.6f/Mathf.Max(.75f,Cam.aspect));
                }
            }
            _rig.MaxSize = closeView!=null?16f:60f;
            if (home)
            {
                var residence = ActiveResidence;
                size = Mathf.Max(HomeRoomView.DefaultZoom, FrameRoom(residence.Width,residence.Depth,0f));
                _rig.MaxSize = Mathf.Max(size,residence.CameraSize);
            }
            else if (interior)
            {
                float dock = Mathf.Min(.48f, GameUi.ShopDockWidth / Mathf.Max(1f, Ui.CanvasWidth));
                size = Mathf.Max(ShopInteriorView.DefaultZoom, FrameRoom(Interior.Width,Interior.Depth,dock));
                _rig.MaxSize = Mathf.Max(size,Interior.CameraSize);
            }
            else if (closeView != null && (InPachinkoRoom || closeView.Place.Model == "office_tower"))
            {
                size = Mathf.Max(9.5f, FrameRoom((float)closeView.Place.Width, (float)closeView.Place.Depth, 0));
                _rig.MaxSize = Mathf.Max(size, _rig.MaxSize);
            }
            _roomOverviewSize = size;
            if(ExpoView?.Open==true)
            {
                _rig.SetInterior(true);_rig.SetPlaceAngles(ExpoView.transform.eulerAngles.y,52f);
                _rig.MinSize=7f;_rig.MaxSize=45f;size=ExpoView.CameraSize(Cam.aspect);
            }
            else if(ExpoView?.MonthlyOpen==true)
            {
                _rig.SetInterior(true);_rig.SetPlaceAngles(ExpoView.transform.eulerAngles.y,52f);
                _rig.MinSize=4.5f;_rig.MaxSize=20f;size=7.5f;
            }
            else if (Session.CurrentPlace?.Id == GameSession.OldMarketVenueId)
            {
                _rig.SetInterior(true); _rig.SetPlaceAngles((float)Session.CurrentPlace.Heading, 52f);
                _rig.MinSize = 7f; _rig.MaxSize = 30f; size = 14f;
            }
            _wasRoomKind = RoomKind;
            if (smooth) _rig.Size = size;
            else _rig.Snap(FocusPoint(size), size);
            if (editing) EnterEditView(true);
        }

        private void OnCityActivitySettled(string activityId) => Ui.OpenCityActivitySettlement(activityId);

        private void OnPopup(string title, string body) => OnEventCgPopup(title, body, null);

        private void OnEventCgPopup(string title, string body, EventCgFrame cg)
        {
            if(Session?.BrandExpoExclusive==true&&title!="你晕倒了"&&title!="住院了"&&!GameSession.IsScheduledPropertyPopup(title)){Ui.Toast(title+"："+body);return;}
#if UNITY_EDITOR
            // This explicit test-only jump spans a week. Keep its ordinary shop
            // notification backlog from covering the exhibition controls.
            if (Session?.DebugExpoFastForwarding == true) return;
#endif
            if (title == "你晕倒了" || title == "住院了")
            {
                Ui.CloseAllWindows();
                _pendingEnter = null;
                CancelHomeBedTrip();
                if (Session.PendingEvent != null) Ui.ShowEvent(Session.PendingEvent);
                Ui.Popup(title, body);
                if (Session.CurrentPlace != null) Ui.OnEnteredPlace(Session.CurrentPlace);
                return;
            }
            Ui.Popup(title, body, cg);
        }

        private void OnTripArrived(string nodeId)
        {
            SoundFx.Play(Sfx.Arrival);
            if (_pendingEnter != null && _pendingEnter.NodeId == nodeId) EnterPlace(_pendingEnter);
            _pendingEnter = null;
        }

        private void OnCustomerArrived(Place shop, PCShopSell.Core.Shop.ShopCustomer c)
        {
            if (InInterior || _crowd == null || !World.Places.TryGetValue(shop.Id, out PlaceVisual pv)) return;
            _crowd.SendShopper((pv.Place.ExplicitEntrance ? pv.Entrance : pv.Entrance + new Vector3(0, 0, StreetWorldBuilder.FrontZAt(pv.Entrance.x) - 0.3f)));
        }

        private void OnShopGuestChanged(Place shop, ShopGuestEvent change)
        {
            if (!InInterior || Session.CurrentPlace != shop || Ui.MainMenuOpen) return;
            SoundFx.Play(change switch
            {
                ShopGuestEvent.Entered => Sfx.Bell,
                ShopGuestEvent.Paid => Sfx.Coin,
                _ => Sfx.ShopGuestLeaves
            });
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
            if (FindObjectOfType<AudioListener>() == null) Cam.gameObject.AddComponent<AudioListener>();
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
