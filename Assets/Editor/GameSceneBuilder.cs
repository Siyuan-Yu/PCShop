using System;
using System.IO;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;
using PCShopSell.Core.Home;
using PCShopSell.Game;
using PCShopSell.Game.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace PCShopSell.EditorTools
{
    /// <summary>
    /// 生成可玩的主场景（Assets/Scenes/Main.unity）并加入 Build Settings；
    /// 以及批处理冒烟测试：不进 Play 模式，直接搭起整个游戏、模拟一段流程、截图。
    /// </summary>
    public static class GameSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Main.unity";

        [MenuItem("PCShopSell/Create Main Scene")]
        public static void CreateSceneFromMenu() => CreateScene();

        public static void CreateSceneFromBatch()
        {
            int code = 0;
            try
            {
                CreateScene();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                code = 1;
            }
            EditorApplication.Exit(code);
        }

        public static void CreateScene()
        {
            UrpSetup.Ensure();
            if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildRig();
            new GameObject("Game").AddComponent<GameController>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("Scene created: " + ScenePath);
        }

        private static Camera BuildRig()
        {
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.allowHDR = true;
            var extra = cam.GetUniversalAdditionalCameraData();
            extra.renderPostProcessing = true;

            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            sunGo.transform.rotation = Quaternion.Euler(42, 115, 0);

            var volGo = new GameObject("PostVolume");
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 1;
            return cam;
        }

        /// <summary>
        /// 校验真正的 Main.unity：能打开、没有丢失脚本、场景里有 GameController 并能搭起整个游戏。
        /// （冒烟测试用的是新建的空场景，发现不了 Main.unity 里脚本引用丢失的问题。）
        /// </summary>
        public static void VerifyMainSceneFromBatch()
        {
            int code = 0;
            try
            {
                Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                int missing = 0;
                foreach (GameObject root in scene.GetRootGameObjects())
                    foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                        missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                var game = UnityEngine.Object.FindObjectOfType<GameController>();
                Debug.Log($"VERIFY missingScripts={missing} gameController={(game != null)}");
                if (missing > 0 || game == null) code = 1;
                else
                {
                    game.Bootstrap(false);
                    Debug.Log($"VERIFY bootstrapped places={game.City.Places.Count} session={(game.Session != null)}");
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                code = 1;
            }
            EditorApplication.Exit(code);
        }

        // ---------- 冒烟测试 ----------

        public static void SmokeFromBatch() => RunSmokeBatch(Smoke);
        public static void AgencyFromBatch() => RunSmokeBatch(SmokeAgency);
        public static void PlacementFromBatch() => RunSmokeBatch(SmokePlacement);

        private static void SmokePlacement()
        {
            _outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../docs/previews"));
            Directory.CreateDirectory(_outDir);
            ShaderUtil.allowAsyncCompilation = false;
            UrpSetup.Ensure();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Camera cam = BuildRig();
            _game = new GameObject("Game").AddComponent<GameController>();
            _game.Bootstrap(false, 7);
            _game.Ui.QuestPopups = false;
            foreach (Canvas c in _game.Ui.Canvases)
            {
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = 1f;
            }
            var s = _game.Session;
            Teleport(s, "agency");
            Check(s.SignLease("player_shop"), "预览：租下小店，获得开局设施");
            Teleport(s, "player_shop");
            Frame(.05f);
            Check(s.BeginRenovation() && _game.BeginPlacement(true), "预览：进入店面装修");
            _game.DebugOrbit(18f, 0);
            _game.SelectStock(FurnitureCatalog.ShopCounter);
            _game.DebugCursorLocal = new Vector3(2.5f, 0, 2.75f);
            for (int i = 0; i < 45; i++) Frame(.05f);
            var outline = UnityEngine.Object.FindObjectOfType<PlacementOutline>();
            Check(outline != null && outline.IsValid && _game.PlaceError == null, "空位置显示可放置轮廓");
            CheckPreviewMaterials(outline, FurnitureCatalog.ShopCounter);
            Shot("vs1_placement_counter_valid");
            _game.DebugCursorLocal = new Vector3(4.5f, 0, 1.0f);
            Frame(.05f);
            Check(!outline.IsValid && _game.PlaceError != null, "挡住门口立即切换不可放置轮廓");
            CheckPreviewMaterials(outline, FurnitureCatalog.ShopCounter);
            Shot("vs1_placement_counter_invalid");
            _game.DebugCursorLocal = new Vector3(2.5f, 0, 2.75f);
            _game.RotateGhost();
            Frame(.05f);
            Check(outline.IsValid && _game.EditGhostAt.Rot == 1, "移回空地、旋转后恢复绿色轮廓");
            Shot("vs1_placement_counter_rotated", 1280, 720);
            typeof(GameController).GetMethod("ConfirmPlace", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(_game, new object[] { FurnitureCatalog.Get(FurnitureCatalog.ShopCounter), _game.EditGhostAt });
            Frame(.05f);
            Check(s.ShopLayout.Any(p => p.Id == FurnitureCatalog.ShopCounter) && UnityEngine.Object.FindObjectOfType<PlacementOutline>() == null,
                "确认摆放后家具正常落地、轮廓立即清除");
            int counter = s.ShopLayout.ToList().FindIndex(p => p.Id == FurnitureCatalog.ShopCounter);
            var counterVisual = _game.Interior.Furniture.Entries.First(e => e.Def.Id == FurnitureCatalog.ShopCounter).Go;
            typeof(GameController).GetMethod("PickUpFurniture", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(_game, new object[] { counter });
            _game.DebugCursorLocal = new Vector3(2.5f, 0, 4.0f);
            Frame(.05f);
            outline = UnityEngine.Object.FindObjectOfType<PlacementOutline>();
            Check(_game.PlaceHoldsPlaced && !counterVisual.activeSelf && outline.IsValid, "拿起原有家具只显示移动预览，原位置暂时隐藏");
            CheckPreviewMaterials(outline, FurnitureCatalog.ShopCounter);
            _game.SelectStock(FurnitureCatalog.Shelf);
            Frame(.05f);
            Check(counterVisual.activeSelf && UnityEngine.Object.FindObjectsOfType<PlacementOutline>().Length == 1, "改选货架时旧柜台恢复、上一件轮廓清除");
            outline = UnityEngine.Object.FindObjectOfType<PlacementOutline>();
            CheckPreviewMaterials(outline, FurnitureCatalog.Shelf);
            _game.DebugCursorLocal = new Vector3(4.5f, 0, 3.75f);
            Frame(.05f);
            Shot("vs1_placement_shelf");
            _game.EndPlacement();
            Check(!UnityEngine.Object.FindObjectsOfType<PlacementOutline>().Any(), "退出装修不残留预览");

            Teleport(s, "shop12");
            s.SendByCourier = true;
            Check(s.BuyFurniture("aircon"), "购买墙面空调用于预览验收");
            s.AdvanceClock(s.CourierQuote(true).minutes + 30);
            s.SendByCourier = false;

            Teleport(s, "home");
            Frame(.05f);
            Check(_game.BeginPlacement(false), "预览：进入家中摆放");
            int stool = s.HomeLayout.ToList().FindIndex(p => p.Id == "stool");
            Check(s.StoreFurniture(false, stool), "把已有圆凳收起再挪动");
            _game.SelectStock("stool");
            _game.DebugCursorLocal = new Vector3(3.5f, 0, 2.5f);
            for (int i = 0; i < 45; i++) Frame(.05f);
            outline = UnityEngine.Object.FindObjectOfType<PlacementOutline>();
            Check(outline != null && outline.IsValid, "家中圆形家具也能预览轮廓");
            CheckPreviewMaterials(outline, "stool");
            Shot("vs1_placement_home_stool");
            _game.SelectStock("aircon");
            _game.DebugCursorLocal = new Vector3(1.25f, 0, 4.5f);
            Frame(.05f);
            outline = UnityEngine.Object.FindObjectOfType<PlacementOutline>();
            Check(outline.IsValid && _game.EditGhostAt.Rot == 0, "墙面家具在可用墙段显示绿边");
            CheckPreviewMaterials(outline, "aircon");
            Shot("vs1_placement_wall_valid");
            _game.DebugCursorLocal = new Vector3(3.25f, 0, 4.5f);
            Frame(.05f);
            Check(!outline.IsValid, "墙面家具挡住窗户显示红边");
            Shot("vs1_placement_wall_invalid");
            _game.EndPlacement();
            _game.DebugCursorLocal = null;
            Check(Resources.FindObjectsOfTypeAll<Material>().All(m => !m.name.StartsWith("PlacementOutline "))
                && Resources.FindObjectsOfTypeAll<Mesh>().All(m => m.name != "PlacementOutline silhouette"), "切换、放下和取消后释放全部预览材质与网格");
            Shader shader = Resources.Load<Shader>("Shaders/PlacementOutline");
            Check(shader != null && !ShaderUtil.ShaderHasError(shader), "Resources 中的轮廓 Shader 正确编译");
        }

        private static void CheckPreviewMaterials(PlacementOutline outline, string id)
        {
            Check(outline != null, "存在家具预览：" + id);
            if (outline == null) return;
            var reference = FurnitureVisuals.Build(null, FurnitureCatalog.Get(id), false);
            var actual = outline.GetComponentsInChildren<Renderer>().Where(r => !r.name.StartsWith("PlacementOutline ")).ToArray();
            var expected = reference.GetComponentsInChildren<Renderer>();
            Check(actual.Length == expected.Length && actual.Zip(expected, (a, b) => a.sharedMaterials.SequenceEqual(b.sharedMaterials)).All(x => x),
                "预览与正常家具使用完全相同的材质：" + id);
            Kill.It(reference);
        }

        private static void RunSmokeBatch(Action run)
        {
            SaveStore.DirectoryOverride = System.IO.Path.GetFullPath("work/smoke-saves");
            int errors = 0;
            Application.LogCallback onLog = (msg, stack, type) =>
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
            };
            Application.logMessageReceived += onLog;
            int code = 0;
            try
            {
                run();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                code = 1;
            }
            Application.logMessageReceived -= onLog;
            if (errors > 0) code = 1;
            Debug.Log($"SMOKE RESULT code={code} errors={errors}");
            EditorApplication.Exit(code);
        }

        private static string _outDir;
        private static GameController _game;

        private static void Smoke()
        {
            _outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../docs/previews"));
            Directory.CreateDirectory(_outDir);
            ShaderUtil.allowAsyncCompilation = false;
            UrpSetup.Ensure();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Camera cam = BuildRig();
            _game = new GameObject("Game").AddComponent<GameController>();
            _game.Bootstrap(false, 7); // Repeatable world/customer sequence; normal play still uses a fresh seed.
            _game.Ui.QuestPopups = false;
            foreach (Canvas c in _game.Ui.Canvases)
            {
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = 1f;
            }

            GameSession s = _game.Session;
            Check(s.CurrentPlace == s.Home, "开局在家");
            Check(!s.HasShop && s.Ledger.Balance.Yuan == 10000, "新游戏：还没有店、现金 ¥10,000");
            _game.Ui.ShowIntro();
            ClickGuideButton("开始经营");
            _game.Ui.DebugRefreshQuests();
            Check(!_game.Ui.AnyModalOpen && !_game.Session.Time.IsHeld, "开始经营直接回到游戏，不自动打开教学目录");
            Check(s.CurrentQuest()?.Id == "rent", "新游戏只派发租铺任务");
            Shot("vs1_guide_start");
            SmokeHomeMotion();
            _game.Ui.OpenQuestWindow();
            Check(GuideButton("前往这一步") == null && GuideButton("跟踪这一步") == null && GuideButton("电脑乐园") == null, "任务条没有快捷前往，也没有章节和任意选任务");
            CheckGuideReadOnly();
            Shot("vs1_guide_first_task");
            ClickGuideButton("知道了");
            Check(s.CurrentPlace == s.Home && s.Trip == null && !s.Time.IsHeld, "关闭租铺说明只回到原地，不发起出行或残留暂停");
            _game.Ui.OpenBusinessPanel();
            Check(!_game.Ui.BusinessUnlocked && !_game.Ui.AnyModalOpen && GuideButton("经营") == null, "开局没有经营总览，顶栏也没有“经营”按钮");
            _game.Ui.CloseAllWindows();
            _game.Ui.ShowIntro();
            Frame(0.05f);
            Shot("vs1_83_intro_no_shop");
            Teleport(s, "agency");
            _game.Ui.OnEnteredPlace(s.CurrentPlace);
            Frame(0.05f);
            Shot("vs1_84_agency");
            SmokeAgencySelection(s);
            _game.Ui.DebugRefreshQuests();
            Check((_game.Ui.QuestTrackerText ?? "").Contains("租一间铺子"), "任务条：新游戏先去租铺子：" + _game.Ui.QuestTrackerText);
            Check(s.Listings.Any(l => l.PlaceId == "player_shop" && s.LeaseBlocker(l) == null), "中介挂着街尾空铺、现在就能签");
            Check(s.Listings.Any(l => s.LeaseBlocker(l) != null && l.OpenDay > s.Clock.Today.TotalDays), "中介有现租户还没到期的铺子");
            Check(PCShopSell.Game.UI.GameUi.PortraitOf(PCShopSell.Core.Session.Rosters.AgentChen.Id) != null, "中介小陈有立绘");
            Check(s.AgentDiscount == 0, "没熟之前中介费按标价");
            Check(s.StaffChat(PCShopSell.Core.Session.Rosters.AgentChen) != null, "和中介小陈闲聊");
            s.Backpack.AddGift(PCShopSell.Core.Home.Gifts.Soda, 1);
            Check(s.GiveStaffGift(PCShopSell.Core.Session.Rosters.AgentChen, PCShopSell.Core.Home.Gifts.Soda), "送中介一瓶饮料");
            Check(s.AgentDiscount > 0, "聊熟了中介费有折扣");
            _game.Ui.CloseAllWindows();
            Teleport(s, "agency");
            _game.Ui.OnEnteredPlace(s.CurrentPlace);
            Frame(0.05f);
            Shot("vs1_93_agency_discount");
            Teleport(s, "home");
            Check(s.DebugRentStarterShop(), "作弊：直接租下街尾小店（回到旧开局）");
            Check(s.HasShop && !s.PlayerShop.Stock.IsEmpty, "作弊后有店、有开局货");
            _game.Ui.CloseAllWindows();
            s.Leave();
            Shot("vs1_01_home_morning");

            _game.GoToPlace(s.PlayerShopPlace);
            long mm0 = s.Clock.Now.TotalMinutes; int ff = 0;
            while (s.Trip == null && ff < 5) { Frame(0.05f); ff++; }
            ff = 0; mm0 = s.Clock.Now.TotalMinutes;
            while (s.Trip != null && ff < 3000) { Frame(0.05f); ff++; }
            double walkRate = (s.Clock.Now.TotalMinutes - mm0) / (ff * 0.05), idleRate = 1.0 / s.Time.SecondsPerMinuteAt1x;
            Log($"TRIPRATE 步行 {walkRate:0.00} 游戏分钟/秒，站着 {idleRate:0.00}");
            Check(walkRate > idleRate * 0.15 && walkRate < idleRate * 0.4, "步行画面速度再次减半，每公里游戏耗时不变");
            Check(s.InPlayerShop, "走到店里并进店");
            Log("到店 " + s.Clock.Now);

            s.SetMarkup(1.1);
            bool bubbled = false;
            for (int i = 0; i < 9000 && !bubbled; i++)
            {
                Frame(0.1f);
                bubbled = _game.Interior.Emotes.Any(e => e.Bubble && (e.Text.Contains("有货") || e.Text.Contains("没货") || e.Text.Contains("配件") || e.Text.Contains("货架上有")));
            }
            Check(bubbled, "顾客进店后头顶有对话气泡：想买什么、有没有货");
            Frame(0.1f);
            Shot("vs1_80_customer_bubble");
            for (int i = 0; i < 400 && s.PlayerShop.Queue.Count == 0; i++) Frame(0.1f);
            Shot("vs1_02_shop_interior");

            // 不去结账：顾客拿着货到柜台排队，等烦了就把货放回货架再走
            for (int i = 0; i < 9000 && s.PlayerShop.Queue.Count == 0; i++) Frame(0.1f);
            Log($"QUEUEDIAG now={s.Clock.Now} open={s.PlayerShop.IsOpen} inShop={s.InPlayerShop} customers={s.PlayerShop.TodayCustomers} lost={s.PlayerShop.TodayLost} rate={s.PlayerShop.ArrivalRatePerMinute(s.Clock.Now) * 60:0.00}/h lf={s.PlayerShop.LocationFactor} traffic={s.PlayerShop.TrafficMultiplier} size={s.PlayerShop.Size} stock={s.PlayerShop.Stock.UsedSpace}");
            Check(s.PlayerShop.Queue.Count > 0, "有顾客拿着货到柜台排队");
            Frame(1.5f);
            Shot("vs1_55_customer_wait");
            // 拉远看整间店：展示桌在正中间，柜台到门口留出排队的路
            _game.Ui.CloseDock();
            _game.DebugShopOverview(true);
            for (int i = 0; i < 8; i++) Frame(0.1f);
            Shot("vs1_70_shop_overview");
            _game.DebugShopOverview(false);
            // 走到店门口：能看到门外的街，按 F 或接着往外走就出门
            _game.Interior.StandAtDoor();
            for (int i = 0; i < 10; i++) Frame(0.1f);
            Check(_game.TryGetInteract(out _, out string doorHint) && doorHint.Contains("出门"), "店门口有“出门”提示");
            Shot("vs1_77_shop_door");
            for (int i = 0; i < 20 && !_game.Interior.OwnerOutside; i++)
            {
                _game.Interior.MoveOwner(UnityEngine.Vector3.back, 0.1f);
                _game.Step(0);
                PCShopSell.Game.Runtime.FigureMotion.AdvanceAll(.1f, true);
            }
            Check(_game.Interior.Root.Find("Owner").GetComponent<PCShopSell.Game.Runtime.FigureMotion>().WalkBlend > .5f, "店内走动播放迈步和摆臂");
            Check(_game.Interior.OwnerOutside, "老板能从店门走出去");
            _game.Interior.StepBackInside();
            _game.Ui.ConfirmLeaveShop();
            Frame(0.05f);
            Check(_game.Ui.AnyModalOpen && s.InPlayerShop, "出门前先问要不要离开（没人看店会关门）");
            Shot("vs1_78_leave_confirm");
            _game.Ui.CloseModalWindows();
            _game.Interior.StandAtCounter();
            Frame(0.1f);
            Check(_game.Interior.NearCounter, "回到柜台");
            bool counterOk = _game.TryGetInteract(out _, out string emptyCounter);
            Log($"COUNTER ok={counterOk} label={emptyCounter} modal={_game.Ui.AnyModalOpen} near={_game.Interior.NearCounter} inShop={_game.InInterior}");
            Check(counterOk && emptyCounter.Contains("收银"), "收银台随时能交互");
            {
                var shelfKinds = _game.Interior.ShelfKindIds;
                Check(shelfKinds.Count == 6 && shelfKinds.Distinct().Count() == 6, $"标准店有 6 个货架，各摆一类配件（{string.Join(",", shelfKinds)}）");
            }
            _game.Ui.OpenDock();
            Frame(0.1f);
            int lostBefore = s.PlayerShop.TodayLost;
            for (int i = 0; i < 9000 && s.PlayerShop.Queue.Count > 0; i++) Frame(0.1f);
            Check(s.PlayerShop.TodayLost > lostBefore, "排太久顾客不买了");
            Frame(0.3f);
            Shot("vs1_56_customer_putback");
            // Checkout is tested with a stocked daytime shop. Sparse starter stock and a random
            // late-night return must not turn this UI test into a multi-day survival simulation.
            Teleport(s, "player_shop");
            s.Clock.Restore(s.Clock.Today, 10 * 60);
            foreach (var p in PartCatalog.All) s.PlayerShop.Stock.ForceAdd(p, true, 2);
            _game.Ui.OnEnteredPlace(s.CurrentPlace);
            _game.Interior.StandAtCounter();
            double checkoutTestRate = s.PlayerShop.Settings.BaseCustomersPerHour;
            s.PlayerShop.Settings.BaseCustomersPerHour = 12;
            int sold = 0;
            for (int i = 0; i < 9000 && sold < 3; i++)
            {
                _game.Ui.CloseModalWindows();
                Frame(0.1f);
                if (s.PlayerShop.Queue.Count > 0)
                {
                    if (sold == 0)
                    {
                        int queued = s.PlayerShop.Queue.Count;
                        _game.Interior.StandAtShelf(0);
                        Frame(0.05f);
                        _game.Checkout(s.PlayerShop.Queue[0].Id);
                        Check(s.PlayerShop.Queue.Count == queued, "老板不在收银台时没法结账，顾客接着排队");
                        _game.Interior.StandAtCounter();
                        Frame(0.05f);
                        Check(_game.TryGetInteract(out _, out string counterHint) && counterHint.Contains("结账"), "柜台边有“给顾客结账”提示");
                        Shot("vs1_57_counter_hint");
                    }
                    var cashBeforeSale = s.Ledger.Balance;
                    _game.Checkout(s.PlayerShop.Queue[0].Id);
                    if (s.Ledger.Balance > cashBeforeSale) sold++;
                }
            }
            s.PlayerShop.Settings.BaseCustomersPerHour = checkoutTestRate;
            Log($"卖出 {sold} 单，现金 {s.Ledger.Balance}，时间 {s.Clock.Now}");
            if (sold == 0)
                Log($"DIAG open={s.PlayerShop.IsOpen} inShop={s.InPlayerShop} customers={s.PlayerShop.TodayCustomers} lost={s.PlayerShop.TodayLost} active={s.PlayerShop.ActiveCustomers.Count} stock={s.PlayerShop.Stock.UsedSpace} fame={s.PlayerShop.Reputation} markup={s.PlayerShop.Markup} trip={(s.Trip != null)}");
            Check(sold > 0, "有顾客结账");

            _game.Ui.RebuildDock();
            Shot("vs1_03_shop_counter");

            // 走到货架前：头顶提示，按 F 看货
            if (!s.InPlayerShop)
            {
                Teleport(s, "player_shop");
                _game.Ui.OnEnteredPlace(s.CurrentPlace);
                Frame(0.3f);
            }
            _game.Interior.StandAtShelf(0);
            _game.Ui.CloseAllWindows();
            Frame(0.05f);
            Check(_game.Interior.NearShelf, "走到货架前能交互");
            Shot("vs1_50_shelf_hint");
            PartCategory shelfCat = PCShopSell.Core.Inventory.ShelfKinds.Get(_game.Interior.NearbyShelfKind).Category;
            int shelfLayoutIdx = _game.Interior.NearbyShelf;
            {
                // 这个货架一件货都没有：3D 货架板上全空 + 红色缺货条
                var emptyCat = shelfCat;
                bool hadAny = PartCatalog.InCategory(emptyCat).Any(p => s.PlayerShop.Stock.CountAll(p) > 0);
                Log("SHELFEMPTY 货架 " + emptyCat + " 开局是否有货：" + hadAny);
                _game.Ui.CloseDock();
                Frame(0.8f);
                Shot("vs1_86_shelf_3d_empty");
                _game.Ui.OpenDock();
            }
            _game.Ui.OpenShelfWindow(shelfLayoutIdx);
            Frame(0.05f);
            Check(_game.Ui.AnyModalOpen, "货架窗口打开");
            Shot("vs1_51_shelf");
            _game.Ui.CloseModalWindows();
            // 摆满货的货架：整盒 / 散件各不同数量，截图后再拿走
            var shelfParts = PartCatalog.InCategory(shelfCat).OrderBy(p => p.Tier).ToList();
            int[] boxedN = { 5, 2, 0 }, looseN = { 3, 7, 1 };
            for (int i = 0; i < shelfParts.Count; i++)
            {
                if (boxedN[i] > 0) s.PlayerShop.Stock.ForceAdd(shelfParts[i], true, boxedN[i]);
                if (looseN[i] > 0) s.PlayerShop.Stock.ForceAdd(shelfParts[i], false, looseN[i]);
            }
            // 店里货架上的 3D 摆货：上层整盒、下两层散件
            _game.Ui.CloseDock();
            _game.Interior.StandAtShelf(0);
            Frame(0.8f);
            Shot("vs1_73_shelf_3d");
            _game.Ui.OpenDock();
            _game.Ui.OpenShelfWindow(shelfLayoutIdx);
            Frame(0.05f);
            Shot("vs1_69_shelf_stocked");
            _game.Ui.CloseModalWindows();
            _game.Ui.DebugSelectTab(1);
            Frame(0.05f);
            Shot("vs1_87_stock_tab");
            for (int i = 0; i < shelfParts.Count; i++)
            {
                if (boxedN[i] > 0) s.PlayerShop.Stock.TryRemove(shelfParts[i], true, boxedN[i]);
                if (looseN[i] > 0) s.PlayerShop.Stock.TryRemove(shelfParts[i], false, looseN[i]);
            }
            foreach (PartCategory tc in new[] { PartCategory.Cpu, PartCategory.Memory, PartCategory.Storage, PartCategory.PowerCase, PartCategory.Motherboard })
            {
                var tps = PartCatalog.InCategory(tc).OrderBy(p => p.Tier).ToList();
                for (int i = 0; i < tps.Count; i++)
                {
                    s.PlayerShop.Stock.ForceAdd(tps[i], true, 2);
                    s.PlayerShop.Stock.ForceAdd(tps[i], false, 3);
                }
                _game.Ui.OpenShelfWindow(s.ShelfIndexes().First(ix => s.ShelfKindAt(ix).Id == PCShopSell.Core.Inventory.ShelfKinds.ForCategory(tc).Id));
                Frame(0.05f);
                Shot("dbg_shelf_" + tc);
                _game.Ui.CloseModalWindows();
                for (int i = 0; i < tps.Count; i++)
                {
                    s.PlayerShop.Stock.TryRemove(tps[i], true, 2);
                    s.PlayerShop.Stock.TryRemove(tps[i], false, 3);
                }
            }
            // 整机展示台：靠近也能按 F 看
            _game.Interior.StandAtDisplay();
            Frame(0.05f);
            Check(_game.Interior.NearDisplay && !_game.Interior.NearShelf, "走到展示台前能交互");
            Check(_game.TryGetInteract(out _, out string displayLabel) && displayLabel.Contains("整机展示"), "展示台头顶有提示");
            Shot("vs1_61_display_hint");
            _game.Ui.OpenDisplayWindow();
            Frame(0.05f);
            Check(_game.Ui.AnyModalOpen, "展示台窗口打开");
            Shot("vs1_62_display");
            // 用库存配件装两台整机摆到店中间的展示台：顾客能直接买走，想买整机的人也变多
            // The preceding sale loop can finish near 04:00. Keep this isolated assembly check
            // in daytime so forced overnight rest cannot move the player home between builds.
            s.Clock.Restore(s.Clock.Today, 10 * 60);
            Feed(s);
            double chanceEmpty = s.PlayerShop.BuildChance;
            for (int tier = 0; tier < 2; tier++)
            {
                foreach (var p in PartCatalog.BuildList(tier)) s.PlayerShop.Stock.ForceAdd(p, true, 1);
                while (s.PlayerShop.DisplayPcs.Count > 0 && s.PlayerShop.DisplayPcs.Count >= s.PlayerShop.DisplaySlots) s.PlayerShop.DisassembleDisplay(s.PlayerShop.DisplayPcs[0]);
                Check(s.AssembleDisplayPc(tier), "装一台整机摆上展示台");
            }
            Check(s.PlayerShop.BuildChance > chanceEmpty * 1.5, "展示台上有整机，想买整机的顾客比例变高");
            _game.Ui.RebuildWindows();
            Frame(0.05f);
            Shot("vs1_81_display_assembled");
            _game.Ui.CloseModalWindows();
            _game.Ui.CloseDock();
            _game.DebugShopOverview(true);
            for (int i = 0; i < 8; i++) Frame(0.6f);
            Shot("vs1_82_display_center");
            _game.DebugShopOverview(false);

            Teleport(s, "player_shop");
            _game.Ui.OnEnteredPlace(s.CurrentPlace);
            s.SetSmartphone(true);
            s.PlaceOnlineOrder("s_mid", new[] { (PartCatalog.Get("ram0"), 3) });

            // 打车去电子市场进货
            _game.Ui.CloseAllWindows();
            s.Leave();
            s.SetVehicle(VehicleKind.Taxi);
            if (s.Clock.MinuteOfDay >= 17 * 60 || s.Clock.Today.IsWeekend)
                s.Clock.Restore(s.Clock.Today.AddDays(s.Clock.Today.IsWeekend ? 2 : 1), 9 * 60);
            _game.GoToPlace(s.City.GetPlace("stall_gpu"));
            for (int i = 0; i < 40; i++) Frame(0.05f);
            Shot("vs1_04_taxi_to_market");
            Run(() => s.Trip == null, 1200);
            Check(s.CurrentPlace != null && s.CurrentPlace.Id == "stall_gpu", "打车到显卡档口");
            Shot("vs1_05_stall_panel");
            s.BuyAtStall(s.CurrentPlace, PartCatalog.Get("gpu0"), 2);
            Log($"进货后随身货 {s.Cargo.UsedSpace} 格");
            _game.Ui.CloseAllWindows();

            // 三轮车 + 交警
            Teleport(s, "dealer");
            s.BuyVehicle(VehicleKind.Tricycle);
            _game.Ui.CloseAllWindows();
            s.Leave();
            s.SetVehicle(VehicleKind.Tricycle);
            foreach (Officer o in s.Police.Officers) o.OnDutyToday = true;
            _game.GoToPlace(s.PlayerShopPlace);
            bool chased = false;
            for (int i = 0; i < 3000 && s.Trip != null; i++)
            {
                Frame(0.05f);
                if (s.Police.AnyChasing && !chased)
                {
                    chased = true;
                    Shot("vs1_06_police_chase");
                }
            }
            Log("三轮车回店，被追过：" + chased + "，现金 " + s.Ledger.Balance);
            _game.Ui.CloseAllWindows();

            // 夜景
            s.Leave();
            s.Clock.Restore(s.Clock.Today, 21 * 60);
            s.ArriveAtNode("e12");
            for (int i = 0; i < 30; i++) Frame(0.05f);
            Shot("vs1_07_street_night");
            {
                // 街上店铺开 / 关门外观：21 点后所有普通店铺放下卷帘门，白天亮灯挂“营业中”
                var shops = _game.World.Places.Values.Where(v => v.Place.Kind == PCShopSell.Core.World.PlaceKind.Shop && v.ClosedOnly.Count > 0 && !s.IsMyShop(v.Place)).ToList();
                Check(shops.Count > 3 && shops.All(v => v.OpenState == 0 && !s.IsPlaceOpen(v.Place)), "夜里 21 点后街上所有店铺都是关门外观");
                Check(shops.All(v => v.ClosedOnly.Any(g => g != null && g.activeSelf) && v.OpenOnly.All(g => g == null || !g.activeSelf)), "关门店铺显示卷帘门、隐藏亮招牌");
                s.Clock.Restore(s.Clock.Today, 12 * 60);
                for (int i = 0; i < 10; i++) Frame(0.05f);
                var openShops = shops.Where(v => s.IsPlaceOpen(v.Place)).ToList();
                Check(openShops.Count > 3 && openShops.All(v => v.OpenState == 1 && v.OpenOnly.Any(g => g != null && g.activeSelf)), "中午街上店铺都是营业外观");
                Shot("vs1_108_street_open");
                s.Clock.Restore(s.Clock.Today, 22 * 60);
                for (int i = 0; i < 10; i++) Frame(0.05f);
                Shot("vs1_109_street_closed");
            }

            // 行情面板
            _game.Ui.OpenMarketPanel();
            Frame(0.05f);
            Shot("vs1_08_market_panel");
            _game.Ui.CloseAllWindows();

            // 装机小游戏
            Teleport(s, "player_shop");
            _game.Ui.OnEnteredPlace(s.CurrentPlace);
            var orderField = s.PlayerShop.Orders.FirstOrDefault();
            if (orderField == null)
            {
                s.PlayerShop.RestoreOrders(new[]
                {
                    new PCShopSell.Core.Shop.BuildOrder(99, 0, s.PlayerShop.QuoteBuild(0), s.Clock.Now, s.Clock.Now.AddMinutes(2880)),
                }, 100);
                orderField = s.PlayerShop.Orders[0];
            }
            foreach (PartDef p in PartCatalog.BuildList(orderField.Tier)) s.PlayerShop.Stock.ForceAdd(p, true, 1);
            _game.Ui.OpenBuildMinigame(orderField);
            Frame(0.05f);
            Shot("vs1_09_build_minigame");
            _game.Ui.CloseAllWindows();
            // 装机台：走到左后角的装机台按 F，开装机单窗口
            _game.Interior.StandAtBench();
            Frame(0.05f);
            Check(_game.Interior.NearBench, "走到装机台前能交互");
            Check(_game.TryGetInteract(out _, out string benchLabel) && benchLabel.Contains("装机台"), "装机台头顶有提示");
            Shot("vs1_71_bench_hint");
            _game.Ui.OpenBenchWindow();
            Frame(0.05f);
            Check(_game.Ui.AnyModalOpen, "装机台窗口打开");
            Shot("vs1_72_bench");
            _game.Ui.CloseAllWindows();
            Check(s.CompleteBuild(orderField.Id, 1), "交付装机订单");

            // 新内容：升级面板 / 随机事件窗口 / 家里的衣橱
            Teleport(s, "player_shop");
            _game.Ui.OnEnteredPlace(s.CurrentPlace);
            _game.Ui.DebugSelectTab(_game.Ui.DebugTabCount - 2);
            Frame(0.05f);
            Shot("vs1_11_upgrades");
            // 按配件定价 / 自动补货 / 经营报表
            s.PlayerShop.SetPartMarkup(PartCatalog.Get("gpu1"), 1.45);
            s.PlayerShop.SetPartMarkup(PartCatalog.Get("ram0"), 1.0);
            s.SetRestockTarget("ram0", 8);
            s.SetRestockTarget("ssd1", 6);
            _game.Ui.DebugSelectTab(2);
            Frame(0.05f);
            Shot("vs1_58_per_part_pricing");
            _game.Ui.OpenWholesaleApp(true);
            Frame(0.05f);
            Check(_game.Ui.AnyModalOpen, "手机 1788 app 的自动补货页打开");
            Shot("vs1_59_auto_restock");
            _game.Ui.CloseModalWindows();
            _game.Ui.OpenWholesaleApp();
            Frame(0.05f);
            Shot("vs1_79_1788_app");
            _game.Ui.CloseModalWindows();
            Check(s.PlayerShop.MarkupOf(PartCatalog.Get("gpu1")) > s.PlayerShop.MarkupOf(PartCatalog.Get("ram0")), "按配件单独定价");
            for (int back = 1; back <= 3 && s.Clock.Today.TotalDays - back >= 0; back++)
            {
                var day = s.Clock.Today.AddDays(-back);
                s.Ledger.Credit(day, "零售", Money.FromYuan(400 + back * 250), "烟雾测试");
                s.Ledger.ForceDebit(day, "进货", Money.FromYuan(300 + back * 120), "烟雾测试");
            }
            _game.Ui.OpenReportWindow();
            Frame(0.05f);
            Check(_game.Ui.AnyModalOpen, "经营报表打开");
            Shot("vs1_60_report");
            _game.Ui.CloseModalWindows();
            _game.Ui.CloseAllWindows();
            s.RaiseEvent("students");
            Frame(0.05f);
            Shot("vs1_12_event");
            Check(s.PendingEvent != null, "事件弹出后等待玩家选择");
            _game.Ui.CloseModalWindows();
            s.ResolveEvent(s.PendingEvent.Choices.Count - 1);
            _game.Ui.CloseModalWindows();
            Check(s.PendingEvent == null, "事件已结算");
            Teleport(s, "home");
            _game.Ui.OnEnteredPlace(s.CurrentPlace);
            Frame(0.05f);
            for (int i = 0; i < 10; i++) Frame(0.05f);
            Check(_game.InHome, "回家后在真实的出租屋里");
            Shot("vs1_13_home");
            // 走到塑料凳旁坐下
            for (int i = 0; i < 14 && _game.HomeRoom.NearbyId != PCShopSell.Game.Runtime.HomeRoomView.Seat; i++)
            {
                _game.HomeRoom.MoveOwner(new Vector3(-0.3f, 0, 1f), 0.1f);
                Frame(0.02f);
            }
            Check(_game.HomeRoom.NearbyId == PCShopSell.Game.Runtime.HomeRoomView.Seat, "走到凳子边能交互");
            _game.HomeInteract(PCShopSell.Game.Runtime.HomeRoomView.Seat);
            Frame(0.05f);
            Check(_game.HomeRoom.IsSitting, "坐下了");
            Shot("vs1_35_sit");
            _game.HomeRoom.MoveOwner(new Vector3(1f, 0, 0), 0.05f);
            Check(!_game.HomeRoom.IsSitting, "一走动就站起来");

            // 坏电风扇：找人上门修
            _game.HomeInteract(PCShopSell.Game.Runtime.HomeRoomView.Repair);
            Frame(0.05f);
            Check(_game.Ui.AnyModalOpen, "上门维修窗口打开");
            Shot("vs1_36_repair");
            _game.Ui.CloseAllWindows();
            s.Clock.Restore(s.Clock.Today, 8 * 60);
            Check(s.BookRepair("fan_broken", (long)s.Clock.Today.TotalDays * 1440 + 13 * 60), "约师傅上门修风扇");
            _game.Ui.OpenSchedulePanel();
            Frame(0.05f);
            Shot("vs1_38_schedule");
            Check(s.HasCalendar == s.HasSmartphone, "日历跟智能手机走");
            _game.Ui.CloseAllWindows();
            s.SetSmartphone(false);
            _game.Ui.OpenSchedulePanel();
            Frame(0.05f);
            Shot("vs1_54_notebook");
            _game.Ui.CloseAllWindows();
            s.SetSmartphone(true);
            int repairEntries = s.Ledger.Entries.Count(e => e.Category == "维修");
            Feed(s);
            s.AdvanceClock(5 * 60 + 10);
            _game.Ui.CloseAllWindows();
            Check(s.RepairmanAtWork != null && s.BrokenHomeItems().Any(), "师傅到了，正在修");
            Frame(2f);
            Frame(2f);
            Shot("vs1_48_repairman");
            _game.Ui.OpenSchedulePanel();
            Frame(0.05f);
            Shot("vs1_49_repairing_schedule");
            _game.Ui.CloseAllWindows();
            s.AdvanceClock(30);
            _game.Ui.CloseAllWindows();
            Check(s.Ledger.Entries.Count(e => e.Category == "维修") == repairEntries, "上门费下单时就付过了，修好不再收钱");
            Check(!s.BrokenHomeItems().Any(), "师傅干完活走了，风扇修好了");
            _game.HomeInteract(PCShopSell.Game.Runtime.HomeRoomView.Wardrobe);
            Frame(0.05f);
            Check(_game.Ui.AnyModalOpen, "衣柜窗口打开");
            Shot("vs1_22_wardrobe");
            _game.Ui.CloseAllWindows();
            s.GrantOutfit("suit");
            Check(s.Equip("suit"), "换上西装");
            _game.HomeInteract(PCShopSell.Game.Runtime.HomeRoomView.Wardrobe);
            Frame(0.2f);
            Shot("vs1_45_wardrobe_suit");
            _game.Ui.CloseAllWindows();
            Frame(0.3f);
            Shot("vs1_46_home_suit");
            s.GrantOutfit("jacket");
            s.Equip("jacket");
            _game.HomeInteract(PCShopSell.Game.Runtime.HomeRoomView.Wardrobe);
            Frame(0.2f);
            Shot("vs1_47_wardrobe_jacket");
            _game.Ui.CloseAllWindows();
            s.Equip(Outfits.Default);
            _game.HomeInteract(PCShopSell.Game.Runtime.HomeRoomView.Bed);
            Frame(0.05f);
            Shot("vs1_23_bed");
            _game.Ui.CloseAllWindows();

            // 纸箱：存取礼品
            s.Backpack.AddGift(PCShopSell.Core.Home.Gifts.Wine, 1);
            s.Backpack.AddGift(PCShopSell.Core.Home.Gifts.Cig, 2);
            Check(s.StoreInBox(PCShopSell.Core.Home.Gifts.Wine) && s.StoreInBox(PCShopSell.Core.Home.Gifts.Cig, 2), "礼品放进纸箱");
            _game.HomeInteract(PCShopSell.Game.Runtime.HomeRoomView.Storage);
            Frame(0.05f);
            Check(_game.Ui.AnyModalOpen, "纸箱窗口打开");
            Shot("vs1_52_storage");
            _game.Ui.CloseAllWindows();
            s.Backpack.AddGift(PCShopSell.Core.Home.Gifts.Cig, 3);
            s.Backpack.AddGift("wine", 2);
            s.Backpack.Food.RiceJin = 10;
            s.Cargo.TryAdd(PartCatalog.Get("gpu0"), true, 2);
            s.Cargo.TryAdd(PartCatalog.Get("ram0"), false, 4);
            _game.Ui.ToggleBackpack();
            Frame(0.05f);
            Check(_game.Ui.AnyModalOpen, "背包窗口打开");
            Shot("vs1_53_backpack_panel");
            _game.Ui.ToggleBackpack();
            Frame(0.05f);
            Check(!_game.Ui.AnyModalOpen, "再按 B 关上背包");
            s.Backpack.ClearGifts();
            s.Backpack.Food.RiceJin = 0;
            s.Cargo.Clear();
            _game.Ui.CloseAllWindows();
            Check(s.TakeFromBox(PCShopSell.Core.Home.Gifts.Wine) && s.TakeFromBox(PCShopSell.Core.Home.Gifts.Cig, 2), "从纸箱拿回礼品");
            s.Backpack.ClearGifts();

            // 电饭锅：真做饭（开局 5 斤米）
            Check(Math.Abs(s.Pantry.RiceJin - 5) < 1e-6, "开局 5 斤米");
            _game.HomeInteract(PCShopSell.Game.Runtime.HomeRoomView.Cooker);
            Frame(0.05f);
            Check(_game.Ui.AnyModalOpen, "电饭锅窗口打开");
            Check(s.CookRice(1, null), "焖一斤米");
            _game.Ui.CloseModalWindows();
            _game.HomeInteract(PCShopSell.Game.Runtime.HomeRoomView.Cooker);
            Frame(0.05f);
            Shot("vs1_25_cooker");
            Check(s.Pantry.Cooking != null && s.Pantry.CookedBowls == 0, "焖饭期间锅里还没有饭");
            _game.Ui.CloseAllWindows();
            s.AdvanceClock(PCShopSell.Core.Home.Pantry.CookMinutes);
            Check(s.Pantry.CookedBowls == 4, "40 分钟后饭焖好了");
            _game.HomeInteract(PCShopSell.Game.Runtime.HomeRoomView.Cooker);
            Frame(0.05f);
            Shot("vs1_37_cooker_ready");
            _game.Ui.CloseAllWindows();
            Frame(0.1f);
            Check(_game.HomeRoom.CookerWorld != null, "屋里有电饭锅的位置");
            Shot("vs1_74_rice_ready");
            int boxes = s.Backpack.Lunchboxes;
            Check(s.PackLunch(s.Pantry.CookedBowls), "饭全部装进饭盒");
            Check(s.Backpack.Lunchboxes == boxes + 4 && s.Pantry.CookedBowls == 0, "饭盒放进背包");
            s.Backpack.Food.RiceJin = 3;
            s.Backpack.AddGift(PCShopSell.Core.Home.Gifts.Soda, 2);
            _game.Ui.ToggleBackpack();
            Frame(0.05f);
            Shot("vs1_75_backpack_lunch");
            _game.Ui.CloseAllWindows();
            Check(s.UnloadBackpack(), "在家一键把背包里的东西放下");
            Check(s.Backpack.Food.RiceJin == 0 && s.Backpack.Lunchboxes == boxes + 4, "米进了厨房，饭盒留在背包");
            s.Backpack.ClearGifts();
            s.Pantry.RiceJin = 4;

            // “我的”信息菜单、网购
            _game.Ui.OpenMyPanel();
            Frame(0.05f);
            Shot("vs1_26_my");
            _game.Ui.CloseAllWindows();
            _game.Ui.OpenOnlinePanel();
            Frame(0.05f);
            Shot("vs1_27_online");
            _game.Ui.CloseAllWindows();

            // 家具城（线下买）、农贸市场
            s.Ledger.Credit(s.Clock.Today, "测试", PCShopSell.Core.Common.Money.FromYuan(20000), "演示");
            s.Clock.Restore(s.Clock.Today, 10 * 60);
            if (!s.SetVehicle(PCShopSell.Core.Session.VehicleKind.Tricycle)) s.SetVehicle(PCShopSell.Core.Session.VehicleKind.Taxi);
            Teleport(s, "shop12");
            _game.Ui.OnEnteredPlace(s.CurrentPlace);
            Frame(0.05f);
            Shot("vs1_28_mall");
            s.SendByCourier = true;
            foreach (string id in new[] { "bed_spring", "aircon", "sofa", "shop_sofa", "shop_led", "bed_good", "bed_good", "bed_good", "aircon" })
                Check(s.BuyFurniture(id), "叫车送 " + id);
            Check(s.CourierRunsHere(false) == 3, "一辆面包车装不下，一共叫了 3 辆");
            Check(s.OpenCourierRun(false).LoadKg <= GameSession.CourierCapacityKg, "每辆车不超载");
            _game.Ui.RebuildWindows();
            Frame(0.05f);
            Shot("vs1_66_mall_courier");
            s.SendByCourier = false;
            foreach (string id in new[] { "curtain", "plant", "rug", "poster", "shop_plant", "shop_poster", "shop_dispenser", "plant" })
            {
                Teleport(s, "home");
                if (s.HasUnloadable) Check(s.UnloadBackpack(), "到家一键放下背着的家具");
                Teleport(s, "shop12");
                Check(s.BuyFurniture(id), "家具城买了装进背包 " + id);
            }
            _game.Ui.RebuildWindows();
            Frame(0.05f);
            Shot("vs1_67_mall_backpack");
            s.AdvanceClock(s.CourierQuote(false).minutes + 10);
            Check(s.FurnitureInStock("bed_good") == 3, "三辆车把家具都送到家了");
            _game.Ui.CloseAllWindows();
            Teleport(s, "shop14");
            _game.Ui.OnEnteredPlace(s.CurrentPlace);
            Frame(0.05f);
            Shot("vs1_29_farm");
            Check(s.BuyIngredient("egg", 2), "农贸市场买鸡蛋");
            Check(s.BuyGift("fruit"), "农贸市场买水果");
            Check(s.Backpack.Food.IngredientCount("egg") == 2, "买的鸡蛋装进背包");
            s.SendByCourier = true;
            double homeRice = s.Pantry.RiceJin;
            Check(s.BuyRice(10), "叫车送 10 斤米回家");
            Check(s.Couriers.Count == 1 && Math.Abs(s.Backpack.Food.RiceJin) < 1e-9, "叫车的米不进背包");
            _game.Ui.OnEnteredPlace(s.CurrentPlace);
            Frame(0.05f);
            Shot("vs1_76_farm_courier");
            s.SendByCourier = false;
            s.AdvanceClock(s.CourierQuote(false).minutes + 5);
            Check(Math.Abs(s.Pantry.RiceJin - homeRice - 10) < 1e-6, "车把米送进了家里厨房");
            _game.Ui.CloseAllWindows();

            // 烟酒茶行、手机店、背包
            Teleport(s, "shop19");
            _game.Ui.OnEnteredPlace(s.CurrentPlace);
            Frame(0.05f);
            Shot("vs1_32_tobacco");
            Check(s.BuyGift("cig"), "烟酒茶行买烟");
            Check(s.BuyGift("wine"), "烟酒茶行买酒");
            {
                var sr = UnityEngine.Object.FindObjectsOfType<UnityEngine.UI.ScrollRect>().FirstOrDefault(x => x.content != null && x.content.rect.height > x.viewport.rect.height + 20);
                Check(sr != null, "烟酒茶行的商品列表能滚动");
                if (sr != null)
                {
                    sr.verticalNormalizedPosition = 0f;
                    float y = sr.content.anchoredPosition.y;
                    _game.Ui.RebuildWindows();
                    Frame(0.05f);
                    Canvas.ForceUpdateCanvases();
                    var sr2 = UnityEngine.Object.FindObjectsOfType<UnityEngine.UI.ScrollRect>().FirstOrDefault(x => x != sr && x.content != null && x.isActiveAndEnabled);
                    if (sr2 != null) Log($"SCROLL content={sr2.content.rect.height:0} view={sr2.viewport.rect.height:0} y={sr2.content.anchoredPosition.y:0}");
                    Check(sr2 != null && Mathf.Abs(sr2.content.anchoredPosition.y - y) < 2f, $"买完东西列表不跳回顶上（{y:0} → {(sr2 != null ? sr2.content.anchoredPosition.y : -1):0}）");
                }
            }
            _game.Ui.CloseAllWindows();
            s.SetSmartphone(false);
            Teleport(s, "shop04");
            _game.Ui.OnEnteredPlace(s.CurrentPlace);
            Frame(0.05f);
            Shot("vs1_33_phone_shop");
            _game.Ui.OpenPhone();
            Frame(0.05f);
            Shot("vs1_39_phone_feature");
            _game.Ui.ClosePhone();
            Check(s.BuySmartphone(), "手机店买智能手机");
            Check(!s.HasApp(PhoneApps.Shop), "新手机没有网购 app");
            _game.Ui.OpenPhone();
            Frame(0.05f);
            Shot("vs1_40_phone_home");
            _game.Ui.CloseAllWindows();
            _game.Ui.OpenPhoneStore();
            Frame(0.05f);
            Shot("vs1_41_phone_store");
            _game.Ui.CloseAllWindows();
            s.Meet(Npcs.Zhang, 20);
            _game.Ui.OpenChatPanel();
            Frame(0.05f);
            Shot("vs1_44_sms");
            _game.Ui.CloseAllWindows();
            Check(s.InstallApp(PhoneApps.Shop) && s.InstallApp(PhoneApps.Chat) && s.InstallApp(PhoneApps.Wholesale), "下载 app");
            s.Meet(Npcs.Zhang, 20);
            _game.Ui.OpenPhone();
            Frame(0.05f);
            Shot("vs1_42_phone_apps");
            _game.Ui.CloseAllWindows();
            _game.Ui.OpenChatPanel();
            Frame(0.05f);
            Shot("vs1_43_chat");
            _game.Ui.CloseAllWindows();
            _game.Ui.OpenMyPanel();
            Frame(0.05f);
            Shot("vs1_34_backpack");
            _game.Ui.CloseAllWindows();

            // 货车明早把大件送到家
            s.Clock.Restore(s.Clock.Today, 23 * 60 + 30);
            s.AdvanceClock(60);
            s.Clock.Restore(s.Clock.Today, 10 * 60);
            _game.Ui.CloseAllWindows();
            Check(s.FurnitureInStock("sofa") == 1 && s.FurnitureInStock("bed_spring") == 1, "货车送到了大件家具");

            // 回家摆家具：先收起硬板床，再摆弹簧床、空调、窗帘、沙发
            Teleport(s, "home");
            _game.Ui.OnEnteredPlace(s.CurrentPlace);
            for (int i = 0; i < 10; i++) Frame(0.05f);
            Check(_game.BeginPlacement(false), "进入摆放模式");
            Check(s.StoreFurniture(false, 0), "收起硬板床");
            _game.SelectStock("sofa");
            _game.DebugCursorLocal = new Vector3(2.75f, 0, 1.0f);
            for (int i = 0; i < 45; i++) Frame(0.05f);
            Check(PCShopSell.Game.Runtime.CameraRig.Pitch > 54f && _game.EditRoom != null, "编辑模式：镜头转成 55° 斜俯视");
            Check(_game.EditHover, "编辑模式：有鼠标坐标读数");
            Shot("vs1_30_placing");
            _game.DebugCursorLocal = null;
            var home = PCShopSell.Core.Home.RoomSpec.Home;
            Check(s.PlaceFromStock(false, "bed_spring", PCShopSell.Core.Home.RoomPlanner.Snap(home, PCShopSell.Core.Home.FurnitureCatalog.Get("bed_spring"), 1.05, 4.0, 0)), "摆弹簧床");
            Check(s.PlaceFromStock(false, "aircon", PCShopSell.Core.Home.RoomPlanner.Snap(home, PCShopSell.Core.Home.FurnitureCatalog.Get("aircon"), 1.25, 0, 0)), "挂空调");
            Check(s.PlaceFromStock(false, "curtain", PCShopSell.Core.Home.RoomPlanner.Snap(home, PCShopSell.Core.Home.FurnitureCatalog.Get("curtain"), 3.15, 0, 0)), "挂窗帘");
            Check(s.PlaceFromStock(false, "sofa", PCShopSell.Core.Home.RoomPlanner.Snap(home, PCShopSell.Core.Home.FurnitureCatalog.Get("sofa"), 2.75, 1.0, 0)), "摆沙发");
            Check(s.PlaceFromStock(false, "rug", PCShopSell.Core.Home.RoomPlanner.Snap(home, PCShopSell.Core.Home.FurnitureCatalog.Get("rug"), 2.75, 2.7, 0)), "铺地毯");
            _game.EndPlacement();
            for (int i = 0; i < 10; i++) Frame(0.05f);
            Check(_game.Ui != null && !_game.Placing, "摆放结束");
            Check(PCShopSell.Game.Runtime.CameraRig.Pitch < 60f, "摆放结束镜头恢复斜视角");
            Shot("vs1_24_home_furnished");
            _game.Ui.CloseAllWindows();

            // 店里摆设：歇业装修一天
            Teleport(s, "player_shop");
            _game.Ui.OnEnteredPlace(s.CurrentPlace);
            Check(s.BeginRenovation(), "歇业装修");
            Check(_game.BeginPlacement(true), "店里进入摆放模式");
            var shopRoom = s.ShopRoom;
            Check(s.PlaceFromStock(true, "shop_sofa", PCShopSell.Core.Home.RoomPlanner.Snap(shopRoom, PCShopSell.Core.Home.FurnitureCatalog.Get("shop_sofa"), 2.3, 0.8, 0), _game.Interior.FixedBlockers()), "摆等候沙发");
            Check(s.PlaceFromStock(true, "shop_plant", PCShopSell.Core.Home.RoomPlanner.Snap(shopRoom, PCShopSell.Core.Home.FurnitureCatalog.Get("shop_plant"), 6.4, 1.0, 0), _game.Interior.FixedBlockers()), "摆绿植");
            Check(s.PlaceFromStock(true, "shop_led", PCShopSell.Core.Home.RoomPlanner.Snap(shopRoom, PCShopSell.Core.Home.FurnitureCatalog.Get("shop_led"), 4.0, 0, 0)), "挂 LED");
            for (int i = 0; i < 60; i++) Frame(0.05f);
            Check(_game.DebugPickWallAt(new UnityEngine.Vector3(4.0f, 2.0f, (float)shopRoom.Depth - 0.02f)) >= 0, "编辑模式：点墙上的 LED 灯屏能选中");
            Check(_game.DebugPickWallAt(new UnityEngine.Vector3(1.0f, 2.0f, (float)shopRoom.Depth - 0.02f)) < 0, "编辑模式：点墙上没家具的地方选不中");
            float yaw0 = PCShopSell.Game.Runtime.CameraRig.Yaw;
            _game.DebugOrbit(40f, -10f);
            for (int i = 0; i < 40; i++) Frame(0.05f);
            Check(System.Math.Abs(PCShopSell.Game.Runtime.CameraRig.Yaw - yaw0) > 30f, "编辑模式：能转视角");
            Shot("vs1_105_edit_rotated");
            _game.EndPlacement();
            {
                // 悬停说明：鼠标停在顶栏“知名度”上，半秒后弹出
                var tip = UnityEngine.Object.FindObjectsOfType<PCShopSell.Game.UI.TipTarget>().FirstOrDefault(t => t.Text != null && t.Text().Contains("饥饿"));
                Check(tip != null, "顶栏状态文字挂了悬停说明");
                PCShopSell.Game.UI.TipTarget.Hover = tip;
                _game.Ui.DebugMouse = new UnityEngine.Vector2(300f, 470f);
                for (int i = 0; i < 20; i++) Frame(0.05f);
                Log("TIPDBG " + _game.Ui.TipDebug);
                Check(_game.Ui.TipVisible, "悬停半秒后弹出说明");
                Shot("vs1_107_tooltip");
                PCShopSell.Game.UI.TipTarget.Hover = null;
                Frame(0.05f);
                _game.Ui.DebugMouse = null;
                Check(_game.Ui.TryHotkey(UnityEngine.KeyCode.M), "快捷键 M 打开“我的”");
                Check(_game.Ui.AnyModalOpen && !_game.Ui.TryHotkey(UnityEngine.KeyCode.P), "开着窗口时其他快捷键不叠窗口");
                _game.Ui.CloseAllWindows();
            }
            _game.BeginPlacement(true);
            _game.DebugOrbit(-40f, 10f);
            for (int i = 0; i < 40; i++) Frame(0.05f);
            Shot("vs1_31_shop_decor");
            _game.EndPlacement();
            _game.Ui.CloseAllWindows();
            Check(!s.PlayerShop.IsOpen, "装修当天店歇业");
            // 存档 / 读档
            Teleport(s, "home");
            SaveStore.Save(s);
            string manualJson = File.ReadAllText(SaveStore.PathOf);
            SaveStore.Save(s, true);
            Check(File.ReadAllText(SaveStore.PathOf) == manualJson, "自动存档不会覆盖手动存档");
            SaveStore.Save(s); // create the backup in the isolated smoke directory
            File.WriteAllText(SaveStore.PathOf, "{broken-json");
            using (var recovered = SaveStore.Load(s.City))
                Check(recovered != null && recovered.Ledger.Balance == s.Ledger.Balance && SaveStore.LastLoadMessage.Contains("备用"), "主存档损坏时可从备份恢复");
            File.WriteAllText(SaveStore.PathOf, manualJson);
            _game.LoadGame();
            Check(_game.Session.Ledger.Balance == s.Ledger.Balance, "读档后余额一致");

            // 快进一个月：睡觉循环
            GameSession g = _game.Session;
            Teleport(g, "home");
            for (int guard = 0; guard < 60 && g.Clock.Today.TotalDays < 31 && !g.IsGameOver; guard++)
            {
                if (g.CurrentPlace != g.Home) Teleport(g, "home");
                g.Clock.Restore(g.Clock.Today, 22 * 60);
                g.Sleep();
                _game.Ui.CloseModalWindows();
            }
            Log($"一个月后：{g.Clock.Now} 现金 {g.Ledger.Balance} 目标评估 {g.MonthGoalEvaluated} 达标 {g.MonthGoalPassed} 结束 {g.IsGameOver}");
            Check(g.MonthGoalEvaluated || g.IsGameOver, "第 30 天评估月目标");
            Frame(0.05f);
            Shot("vs1_10_month_end");

            // 在房产中介换到中型铺子（搬店）+ 目标与人脉面板
            g.Ledger.Credit(g.Clock.Today, "测试", PCShopSell.Core.Common.Money.FromYuan(500000), "演示用");
            g.PlayerShop.Reputation = 50;
            g.Clock.Restore(new PCShopSell.Core.Time.GameDate(130), 10 * 60);
            g.DebugAddListing("shop02", 130, 160);
            Teleport(g, "agency");
            Check(g.SignLease("shop02"), "在中介签下中型铺子，搬店");
            Check(g.PlayerShopPlace.Id == "shop02" && g.PlayerShop.Size == PCShopSell.Core.Shop.ShopSize.Medium, "店搬到了中型铺子");
            Check(g.IsVacant(g.City.GetPlace("player_shop")), "旧铺子变成招租");
            _game.Ui.CloseModalWindows();
            Teleport(g, "agency");
            g.Leave();
            g.ArriveAtNode("e19");
            _game.Ui.CloseAllWindows();
            _game.SnapCamera();
            for (int i = 0; i < 6; i++) Frame(0.05f);
            Shot("vs1_85_street_vacant");
            _game.Ui.OpenResidentTalk(Residents.Get("res_wang"), null);
            Frame(0.05f);
            Check(_game.Ui.AnyModalOpen, "能和街上的人说话");
            Shot("vs1_123_passerby_talk");
            _game.Ui.CloseAllWindows();
            {
                Place phoneShop = g.City.Places.First(p => p.Business == GameSession.PhoneBusiness);
                Teleport(g, phoneShop.Id);
                _game.Ui.OnEnteredPlace(phoneShop);
                Frame(0.05f);
                Shot("vs1_124_phone_shop_upgrades");
                _game.Ui.CloseAllWindows();
            }
            Teleport(g, "shop02");
            Check(g.InPlayerShop, "走进新铺子就是自己的店");
            _game.Ui.CloseModalWindows();
            Frame(0.05f);
            Shot("vs1_14_medium_shop");
            // 新签的铺子是毛坯：设施要去家具城买、回店里歇业装修时摆好，摆完第二天才开业
            Check(!g.ShopFitted && g.ShopLayout.Count == 0, "新签的铺子是毛坯：一件设施都没有");
            Check(!g.PlayerShop.IsOpen && g.PlayerShop.NotFitted, "毛坯铺子开不了业");
            _game.Ui.OpenDock();
            Frame(0.1f);
            _game.Ui.DebugRefreshQuests();
            Check(g.CurrentQuest() == null || (g.CurrentQuest().Id != "rent" && g.CurrentQuest().Id != "buy"), "搬家不重置已完成教学");
            Shot("vs1_88_bare_shop");
            _game.Ui.OpenQuestWindow();
            Frame(0.05f);
            Check(_game.Ui.AnyModalOpen, "点任务条打开这一步的说明");
            Shot("vs1_100_quest_window");
            _game.Ui.CloseModalWindows();
            _game.Ui.CloseAllWindows();
            _game.Ui.CloseDock();
            Teleport(g, "shop12");
            g.SendByCourier = true;
            {
                var fixtureIds = new System.Collections.Generic.List<string> { PCShopSell.Core.Home.FurnitureCatalog.ShopCounter, PCShopSell.Core.Home.FurnitureCatalog.ShopDisplay, PCShopSell.Core.Home.FurnitureCatalog.ShopDisplay, PCShopSell.Core.Home.FurnitureCatalog.ShopBench };
                for (int k = 0; k < 5; k++) fixtureIds.Add(PCShopSell.Core.Home.FurnitureCatalog.Shelf); // 5 个普通货架 + 加固 + 重型（各摆一类货）
                fixtureIds.Add(PCShopSell.Core.Home.FurnitureCatalog.RackStrong);
                fixtureIds.Add(PCShopSell.Core.Home.FurnitureCatalog.RackHeavy);
                foreach (string id in fixtureIds) Check(g.BuyFurniture(id), "家具城买店面设施 " + id);
            }
            _game.Ui.OnEnteredPlace(g.CurrentPlace);
            Frame(0.05f);
            _game.Ui.DebugRefreshQuests();
            Check(g.CurrentQuest() == null || g.CurrentQuest().Id != "buy", "追加家具不重置采购教学");
            Shot("vs1_89_mall_fixtures");
            _game.Ui.CloseAllWindows();
            g.SendByCourier = false;
            g.AdvanceClock(g.CourierQuote(false).minutes + g.CourierQuote(true).minutes + 30);
            Check(g.FurnitureInStock(PCShopSell.Core.Home.FurnitureCatalog.ShopCounter) >= 1 && g.FurnitureInStock(PCShopSell.Core.Home.FurnitureCatalog.ShopDisplay) >= 2, "货车把设施送到了（还有旧店搬来的那套）");
            Teleport(g, "shop02");
            _game.Ui.CloseModalWindows();
            Check(g.BeginRenovation(), "进店歇业装修");
            Check(_game.BeginPlacement(true), "店里进入摆放模式");
            {
                var room = g.ShopRoom;
                bool PutFixture(string id, double x, double z, int rot) =>
                    g.PlaceFromStock(true, id, PCShopSell.Core.Home.RoomPlanner.Snap(room, PCShopSell.Core.Home.FurnitureCatalog.Get(id), x, z, rot));
                Check(PutFixture(PCShopSell.Core.Home.FurnitureCatalog.ShopCounter, 0.9, 4.95, 3), "摆收银台");
                Check(!g.ShopFitted, "只有收银台还不能开业");
                _game.SelectStock(PCShopSell.Core.Home.FurnitureCatalog.Shelf);
                _game.DebugCursorLocal = new Vector3(5.5f, 0, 3.2f);
                for (int i = 0; i < 45; i++) Frame(0.05f);
                Shot("vs1_90_placing_fixtures");
                _game.DebugCursorLocal = null;
                var cats = PCShopSell.Core.Home.FurnitureCatalog.ShelfCategories;
                double[] backX = { 2.5, 4.5, 6.5 }, sideZ = { 2.5, 4.5, 6.5 };
                for (int k = 0; k < 3; k++) Check(PutFixture(PCShopSell.Core.Home.FurnitureCatalog.Shelf, backX[k], 8.8, 0), "摆后墙货架 " + k);
                for (int k = 0; k < 3; k++) Check(PutFixture(k == 2 ? PCShopSell.Core.Home.FurnitureCatalog.RackStrong : PCShopSell.Core.Home.FurnitureCatalog.Shelf, 11.8, sideZ[k], 1), "摆右墙货架 " + k);
                Check(PutFixture(PCShopSell.Core.Home.FurnitureCatalog.ShopDisplay, 4.5, 4.5, 0), "摆展示桌 1");
                Check(PutFixture(PCShopSell.Core.Home.FurnitureCatalog.ShopDisplay, 7.5, 4.5, 0), "摆展示桌 2");
                Check(PutFixture(PCShopSell.Core.Home.FurnitureCatalog.ShopBench, 0.45, 8.1, 3), "摆装机操作台");
                Check(PutFixture(PCShopSell.Core.Home.FurnitureCatalog.RackHeavy, 8.5, 8.8, 0), "摆第 7 个货架（重型）");
            }
            Check(g.ShopFitted && g.ShopFitGap() == null, "收银台和货架摆好了");
            Check(g.ShelfIndexes().Count == 7 && g.ShelfIndexes().All(ix => g.ShelfKindAt(ix) == null), "新摆的货架都还没设置摆什么");
            Check(g.PlayerShop.DisplaySlots == 4, "两张展示桌 = 4 个机位");
            for (int i = 0; i < 10; i++) Frame(0.05f);
            Shot("vs1_91_fixtures_placed");
            _game.EndPlacement();
            _game.Ui.CloseAllWindows();
            for (int i = 0; i < 10; i++) Frame(0.05f);
            Check(!g.PlayerShop.IsOpen, "摆完当天还在歇业装修");
            Check(_game.Interior.ShelfCount == 7, "店内场景按摆放认出 7 个货架");
            _game.Interior.StandAtCounter();
            Frame(0.05f);
            Check(_game.Interior.NearCounter, "站到摆好的收银台后面能收银");
            _game.Interior.StandAtBench();
            Frame(0.05f);
            Check(_game.Interior.NearBench, "站到摆好的操作台前能装机");
            _game.Interior.StandAtDisplay();
            Frame(0.05f);
            Check(_game.Interior.NearDisplay, "站到摆好的展示桌前能看整机");
            _game.Interior.StandAtDoor();
            Frame(0.05f);
            _game.Ui.DebugRefreshQuests();
            Check(g.ShopFitted, "摆放后满足最低开业条件");
            Shot("vs1_92_fitted_shop");
            g.Leave();
            g.AdvanceClock(24 * 60);
            Teleport(g, "shop02");
            Check(g.PlayerShop.IsOpen && !g.IsRenovating, "摆完第二天开业");
            _game.Ui.CloseModalWindows();
            // 货架：新摆的都是空架子，走到货架前按 F 设置摆什么（只能摆一种货）
            _game.Ui.CloseDock();
            _game.Interior.StandAtShelf(0);
            Frame(0.8f);
            Check(_game.Interior.NearShelf && _game.Interior.NearbyShelfKind == null, "新货架还没设置摆什么");
            Check(_game.TryGetInteract(out _, out string unsetHint) && unsetHint.Contains("还没设置"), "头顶提示：还没设置摆什么：" + unsetHint);
            Shot("vs1_97_shelf_unset_3d");
            _game.Ui.OpenShelfWindow(g.ShelfIndexes()[0]);
            Frame(0.05f);
            Shot("vs1_98_shelf_unset_window");
            _game.Ui.CloseModalWindows();
            var shelfIdx = g.ShelfIndexes();
            var shelfCatsAll = PCShopSell.Core.Home.FurnitureCatalog.ShelfCategories;
            for (int k = 0; k < 6; k++) Check(g.SetShelfKind(shelfIdx[k], PCShopSell.Core.Inventory.ShelfKinds.ForCategory(shelfCatsAll[k]).Id), "设置货架 " + k);
            Check(g.SetShelfKind(shelfIdx[6], "drink"), "第 7 个货架摆饮料");
            Frame(0.5f);
            Check(_game.Interior.ShelfKindIds.Count(x => x != null) == 7, "店内场景认出 7 个货架都设置了");
            // 杂货：什么礼品都能放进店里，有对应货架才摆得上去卖；来买配件的人多半不买
            g.ShopLocker.AddGift(PCShopSell.Core.Home.Gifts.Soda, 6);
            g.ShopLocker.AddGift(PCShopSell.Core.Home.Gifts.Cig, 3);
            g.ShopLocker.AddGift(PCShopSell.Core.Home.Gifts.Fruit, 1);
            Check(g.ListGoods(PCShopSell.Core.Home.Gifts.Soda, 6) && g.ListGoods(PCShopSell.Core.Home.Gifts.Cig, 2), "饮料、烟放进店里");
            Check(g.PlayerShop.OnShelfGoods(PCShopSell.Core.Home.Gifts.Soda) == 6 && g.PlayerShop.OnShelfGoods(PCShopSell.Core.Home.Gifts.Cig) == 0, "饮料货架摆上了饮料，没有烟货架烟就摆不上");
            g.SetGoodsMarkup(PCShopSell.Core.Home.Gifts.Cig, 1.3);
            _game.Interior.StandAtShelf(6);
            Frame(0.8f);
            Check(_game.Interior.NearShelf && _game.Interior.NearbyShelfKind == "drink", "走到饮料货架前能交互");
            Check(_game.TryGetInteract(out _, out string goodsHint) && goodsHint.Contains("饮料") && goodsHint.Contains("6"), "货架头顶提示写明几件：" + goodsHint);
            Shot("vs1_94_goods_shelf_3d");
            _game.Ui.OpenShelfWindow(g.ShelfIndexes()[6]);
            Frame(0.05f);
            Check(_game.Ui.AnyModalOpen, "货架窗口打开");
            Shot("vs1_95_goods_shelf");
            _game.Ui.CloseModalWindows();
            _game.Ui.OpenDock();
            _game.Ui.DebugSelectTab(1);
            Frame(0.05f);
            Shot("vs1_96_stock_tab_counts");
            _game.Ui.DebugSelectTab(5);
            Frame(0.05f);
            Shot("vs1_99_upgrades_tab");
            Check(!g.BuyUpgrade(PCShopSell.Core.Shop.ShopUpgrades.Sign), "店里的升级页只能看、不能买");
            _game.Ui.DebugSelectTab(0);
            g.PlayerShop.SetHours(9 * 60, 21 * 60);
            _game.Ui.DebugSelectTab(0);
            Frame(0.05f);
            Shot("vs1_120_shop_hours");
            Check(g.PlayerShop.InHours(g.Clock.Now) == (g.Clock.MinuteOfDay >= 9 * 60 && g.Clock.MinuteOfDay < 21 * 60), "营业时间生效");
            g.PlayerShop.SetHours(0, 24 * 60);
            _game.Ui.CloseDock();
            {
                _game.Interior.StandAtShelf(6);
                Frame(0.1f);
                Vector3 target = _game.Interior.NearbyShelfWorld - new Vector3(0, 1.3f, 0);
                Ray ray = _game.Cam.ScreenPointToRay(_game.Cam.WorldToScreenPoint(target));
                Check(_game.Interior.PickFixture(ray, out int pickIx, out _, out _) == FixturePick.Shelf && pickIx == g.ShelfIndexes()[6], "鼠标指着货架能选中它（左键打开）");
            }
            _game.Ui.DebugQuestPopup();
            Frame(0.05f);
            Shot("vs1_121_quest_popup");
            _game.Ui.CloseModalWindows();
            {
                var keepDay = g.Clock.Today;
                int keepMin = g.Clock.MinuteOfDay;
                g.Clock.Restore(keepDay, 2 * 60 + 30);
                Frame(0.02f);
                Shot("vs1_122_sleep_banner");
                g.Clock.Restore(keepDay, keepMin);
                Frame(0.02f);
            }
            _game.Ui.OpenDock();
            _game.Ui.DebugSelectTab(4);
            Frame(0.05f);
            Shot("vs1_103_renovate_tab");
            _game.Ui.DebugSelectTab(1);
            _game.Ui.OpenShelfKindChooser(g.ShelfIndexes()[0]);
            Frame(0.05f);
            Shot("vs1_104_shelf_kind_chooser");
            _game.Ui.CloseModalWindows();
            Check(g.PlayerShop.ShelfCapacityOf(PCShopSell.Core.Inventory.ShelfKinds.Get("drink")) == 160 && g.PlayerShop.Stock.Capacity >= PCShopSell.Core.Shop.ShopSettings.Capacity(g.PlayerShop.Size) + 30, "重型货架能放 160 公斤，加固 + 重型让后仓多 30 格");
            _game.Ui.CloseAllWindows();
            g.Meet(PCShopSell.Core.Session.Npcs.Zhang, 40);
            g.Meet(PCShopSell.Core.Session.Npcs.Lin, 62);
            g.Meet(PCShopSell.Core.Session.Npcs.Landlady, 20);
            _game.Ui.OpenGoalsPanel();
            Frame(0.05f);
            Shot("vs1_15_goals");
            _game.Ui.CloseAllWindows();

            // 进货渠道的人：档口老板、约出去玩、小道消息
            var d = new PCShopSell.Core.Time.GameDate(131);
            while (d.IsWeekend) d = d.AddDays(1);
            g.Clock.Restore(d, 10 * 60);
            Teleport(g, "stall_gpu");
            Check(!g.IsMet(PCShopSell.Core.Session.Npcs.Wang), "进档口、买东西不会自动存老板的联系方式");
            _game.Ui.CloseModalWindows();
            _game.Ui.OpenNpcWindow(PCShopSell.Core.Session.Npcs.Get(PCShopSell.Core.Session.Npcs.Wang), g.CurrentPlace, null);
            Frame(0.05f);
            Shot("vs1_101_ask_contact");
            _game.Ui.CloseModalWindows();
            Check(g.AskContact(PCShopSell.Core.Session.Npcs.Wang) && g.IsMet(PCShopSell.Core.Session.Npcs.Wang), "聊天时当面要联系方式");
            g.AddRelation(PCShopSell.Core.Session.Npcs.Wang, 45);
            g.AskForTip(PCShopSell.Core.Session.Npcs.Wang);
            _game.Ui.CloseModalWindows();
            _game.Ui.OnEnteredPlace(g.CurrentPlace);
            Frame(0.05f);
            Shot("vs1_16_stall_owner");
            var gpuClerk = PCShopSell.Core.Session.Rosters.GpuClerk;
            Check(g.StaffChat(gpuClerk) != null, "和店员闲聊");
            g.Backpack.AddGift(PCShopSell.Core.Home.Gifts.Soda, 1);
            Check(g.GiveStaffGift(gpuClerk, PCShopSell.Core.Home.Gifts.Soda), "送店员一瓶背包里的饮料");
            Check(g.StaffRapport(gpuClerk.Id) >= 3, "店员熟络度涨了");
            _game.Ui.OpenGreeterDialogue(g.CurrentPlace);
            Frame(0.05f);
            Shot("vs1_65_staff_talk");
            _game.Ui.CloseTopWindow();
            _game.Ui.OpenNpcWindow(PCShopSell.Core.Session.Npcs.Get(PCShopSell.Core.Session.Npcs.Wang), g.CurrentPlace, null);
            Frame(0.05f);
            Check(PCShopSell.Game.UI.GameUi.PortraitOf(PCShopSell.Core.Session.Npcs.Wang) != null, "有人物立绘");
            Shot("vs1_63_npc_talk");
            _game.Ui.CloseModalWindows();
            string lifeStory = g.LifeTalk(PCShopSell.Core.Session.Npcs.Wang);
            Check(!string.IsNullOrEmpty(lifeStory), "聊聊人生能听到故事");
            _game.Ui.OpenNpcWindow(PCShopSell.Core.Session.Npcs.Get(PCShopSell.Core.Session.Npcs.Wang), g.CurrentPlace, null, lifeStory);
            Frame(0.05f);
            Shot("vs1_64_npc_life");
            _game.Ui.CloseModalWindows();
            _game.Ui.OpenInvitePanel(PCShopSell.Core.Session.Npcs.Wang);
            Frame(0.05f);
            Shot("vs1_17_invite");
            _game.Ui.CloseAllWindows();
            _game.Ui.OpenMarketPanel();
            Frame(0.05f);
            Shot("vs1_18_tips");
            _game.Ui.CloseAllWindows();

            // WASD 自由走路（街道范围内任意方向），靠近店门时提示按 F
            g.Leave();
            g.SetVehicle(PCShopSell.Core.Session.VehicleKind.Walk);
            g.ArriveAtNode("e05");
            g.Clock.Restore(g.Clock.Today, 14 * 60);
            bool walked = false;
            foreach (var dir in new[] { (1.0, 0.0), (-1.0, 0.0), (0.0, 1.0), (0.0, -1.0) })
            {
                g.SetSteer(dir.Item1, dir.Item2);
                for (int i = 0; i < 30; i++) Frame(0.05f);
                if (g.PlayerNodeId != "e05" || g.OffNode) { walked = true; break; }
            }
            g.SetSteer(0, 0);
            Check(walked, "WASD 能在街上走起来");
            Check(UnityEngine.Object.FindObjectsOfType<PCShopSell.Game.Runtime.FigureMotion>().Any(m => m.transform.parent != null && m.transform.parent.name == "Player" && m.WalkBlend > .5f), "街道角色实际移动时播放迈步动画");
            for (int i = 0; i < 10; i++) Frame(0.05f);
            Shot("vs1_19_wasd_street");
            // 自己租的铺子门前有青色光柱，一眼能认出来
            g.ArriveAtNode(g.City.GetPlace(g.PlayerShopPlace.Id).NodeId);
            g.Clock.Restore(g.Clock.Today, 19 * 60);
            _game.SnapCamera();
            for (int i = 0; i < 10; i++) Frame(0.05f);
            Check(_game.ShopBeaconVisible, "自己的铺子门前有醒目标记");
            Shot("vs1_102_player_shop_beacon");
            g.Clock.Restore(g.Clock.Today, 14 * 60);
            g.ArriveAtNode("e06");
            Frame(0.05f);
            Check(g.NearbyPlace != null, "走到门口附近能进门");
            Shot("vs1_20_press_e");

            // 自由视角：转到另一侧、俯仰，并在人行道边自由走
            PCShopSell.Game.Runtime.CameraRig.Yaw = 120f;
            PCShopSell.Game.Runtime.CameraRig.Pitch = 50f;
            g.SetSteer(0, 1);
            for (int i = 0; i < 20; i++) Frame(0.05f);
            g.SetSteer(0, 0);
            _game.SnapCamera();
            for (int i = 0; i < 5; i++) Frame(0.05f);
            Shot("vs1_21_free_camera");
            PCShopSell.Game.Runtime.CameraRig.Yaw = 45f;
            PCShopSell.Game.Runtime.CameraRig.Pitch = 35f;

            // Dedicated staff UI and runtime models; fixture is saved only in the isolated smoke directory.
            var staffSave = g.CreateSave();
            staffSave.hasEmployee = true;
            staffSave.employee = new SaveEmployee { name = "收银小陈", wage = 150, checkoutMinutes = 2 };
            staffSave.hasAssembler = true;
            staffSave.assembler = new SaveEmployee { name = "装机小王", wage = 210, checkoutMinutes = 3, buildsCompleted = 8 };
            File.WriteAllText(SaveStore.PathOf, JsonUtility.ToJson(staffSave));
            _game.LoadGame();
            var staffed = _game.Session;
            staffed.Clock.Restore(staffed.Clock.Today, 14 * 60);
            Teleport(staffed, staffed.PlayerShopPlace.Id);
            Frame(0.05f);
            _game.Ui.OpenDock();
            _game.Ui.DebugSelectTab(6);
            Frame(0.05f);
            Check(staffed.Employee != null && staffed.Assembler != null, "两个岗位真实 JSON 存档后都能恢复");
            Shot("vs1_106_dual_staff");

            // Business systems exercise the actual session and UI, in the isolated smoke save.
            _game.Ui.CloseAllWindows();
            staffed.Ledger.Credit(staffed.Clock.Today, "验收", Money.FromYuan(1000000), "经营系统验收资金");
            staffed.PlayerShop.Reputation = 40;
            staffed.Meet(Npcs.Zhou, 80);
            Check(staffed.SignDirectAgreement(), "直供协议解锁");
            Check(staffed.OrderDirect("cpu0", 3, true), "直供赊购产生在途和尾款");
            staffed.RefreshEnterpriseOffer();
            var contract = staffed.EnterpriseContracts.FirstOrDefault(x => !x.Accepted);
            Check(contract != null && staffed.AcceptEnterprise(contract.Id), "企业订单进入装机队列");
            Check(staffed.StartCreatorCampaign("momo"), "创作者推广启用");
            Check(staffed.TradeFund(10, true), "指数投资有持仓");
            var agency = staffed.City.FirstOfKind(PlaceKind.Agency);
            Teleport(staffed, agency.Id);
            _game.Ui.CloseAllWindows();
            var unit = staffed.Listings.FirstOrDefault(x => x.PlaceId != staffed.PlayerShopPlace.Id);
            if (unit != null)
            {
                staffed.DebugAddListing(unit.PlaceId, staffed.Clock.Today.TotalDays, staffed.Clock.Today.TotalDays + 30, Money.FromYuan(1800));
                Check(staffed.OpenVenture(unit.PlaceId, "branch"), "分店占用真实铺面");
            }
            Check(staffed.BuyProperty(staffed.PlayerShopPlace.Id, true), "主店按揭购置物业");
            Check(staffed.ChangeHomeTier(2), "升级安静公寓");
            staffed.Ledger.Credit(staffed.Clock.Today, "验收", Money.FromYuan(2000000), "园区验收资金");
            var parkSite = staffed.City.Places.FirstOrDefault(x => x.Kind == PlaceKind.Shop && x.Size == PCShopSell.Core.Shop.ShopSize.Large && !staffed.IsMyShop(x) && staffed.VentureAt(x.Id) == null && staffed.PropertyAt(x.Id) == null);
            if (parkSite != null)
            {
                staffed.DebugAddListing(parkSite.Id, staffed.Clock.Today.TotalDays, staffed.Clock.Today.TotalDays + 30, Money.FromYuan(4000));
                Check(staffed.BuyPark(parkSite.Id), "园区买地改造");
                staffed.Clock.Restore(staffed.Clock.Today.AddDays(7), 14 * 60);
                Check(staffed.OpenParkUnit(0, "flowers"), "园区花店入驻");
                Check(staffed.OpenParkUnit(1, "food"), "园区餐饮入驻");
                Check(staffed.ParkTrafficBonus(staffed.ParkUnit(0)) > 1, "不同业态相邻导流");
            }
            Check(staffed.FoundMcn(), "MCN 工作室成立");
            staffed.Creator("momo").Relationship = 40;
            staffed.Creator("momo").Resources = 60;
            staffed.Creator("momo").Affection = 60;
            Check(staffed.SignMcnCreator("momo"), "MCN 创作者合约");
            for (int tab = 0; tab < 7; tab++)
            {
                _game.Ui.CloseAllWindows();
                _game.Ui.DebugOpenBusiness(tab);
                Frame(0.05f);
                Shot("vs1_business_" + tab);
            }
            _game.Ui.CloseAllWindows();
            Teleport(staffed, staffed.Home.Id);
            Frame(0.05f);
            Shot("vs1_business_home");
            var dealer = staffed.City.FirstOfKind(PlaceKind.Dealer);
            Teleport(staffed, dealer.Id);
            staffed.Clock.Restore(staffed.Clock.Today, 14 * 60);
            Check(staffed.BuyVehicle(VehicleKind.Van), "购入合法小货车");
            Check(staffed.SetVehicle(VehicleKind.Van), "切换合法小货车");
            _game.Ui.CloseAllWindows();
            staffed.Leave();
            Frame(0.05f);
            Frame(0.4f);
            Shot("vs1_business_van");
            _game.Ui.DebugOpenCreator("momo");
            Frame(0.05f);
            Shot("vs1_business_creator");
            _game.Ui.CloseAllWindows();
            var businessSave = staffed.CreateSave();
            var roundTrip = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(businessSave));
            using (var restored = GameSession.Load(staffed.City, roundTrip))
            {
                Check(restored.TotalDebt == staffed.TotalDebt && restored.NetWorth() == staffed.NetWorth(), "经营资产负债真实 JSON 往返守恒");
                Check(restored.Vehicle == VehicleKind.Van && restored.HomeTier == 2, "住房与载具恢复");
            }

            // Tutorials explain the normal controls; they never travel or select a gameplay page.
            Teleport(staffed, staffed.City.Places.First(p => p.Business == GameSession.PhoneBusiness).Id);
            if (!staffed.HasUpgrade(PCShopSell.Core.Shop.ShopUpgrades.Qr)) Check(staffed.BuyUpgrade(PCShopSell.Core.Shop.ShopUpgrades.Qr), "去手机店开通收款码后才接续经营教学");
            staffed.Leave();
            Check(staffed.CurrentQuest()?.Id == "pricing", "基础开店结束自然派发定价教学");
            _game.Ui.OpenQuestWindow();
            CheckGuideReadOnly();
            Shot("vs1_guide_route");
            ClickGuideButton("知道了");
            Check(!staffed.InPlayerShop && staffed.Trip == null && !_game.Ui.DockOpen, "阅读定价说明不自动回店或打开页面");
            Teleport(staffed, staffed.PlayerShopPlace.Id);
            _game.Ui.OpenDock();
            ClickGuideButton("› 定价");
            Check(staffed.InPlayerShop && _game.Ui.DockOpen && GuideButton("已了解，完成这一步") != null, "玩家手动进入定价页后才显示阅读确认");
            Shot("vs1_guide_pricing");
            var guideCash = staffed.Ledger.Balance;
            ClickGuideButton("已了解，完成这一步");
            Check(staffed.QuestCompleted("pricing") && staffed.Ledger.Balance == guideCash, "阅读定价完成教学，不自动改价或扣钱");
            _game.Ui.CloseAllWindows();
            Check(!staffed.Time.IsHeld, "关闭教学及店铺页后释放暂停");

            Check(staffed.CurrentQuest()?.Id == "restock", "已经交付整机后自然派发补货教学");
            _game.Ui.OpenQuestWindow();
            CheckGuideReadOnly();
            ClickGuideButton("知道了");
            ClickGuideButton("手机");
            ClickGuideButton("<size=34><b>批</b></size>\n<size=13>1788 进货</size>");
            ClickGuideButton("自动补货");
            Check(GuideButton("进货") != null && GuideButton("自动补货") != null && GuideButton("回主店柜台，继续营业 / 等待") == null, "从手机手动进入补货页，页面教学没有回店捷径");
            Shot("vs1_guide_restock");
            _game.Ui.CloseAllWindows();
            Check(staffed.InPlayerShop && !staffed.Time.IsHeld && !_game.Ui.AnyModalOpen, "手动关闭补货页后原地继续经营，无残留暂停");

            // Developed-save fixture: earlier lessons finished, and the park has actually been bought.
            foreach (var q in Quests.All.TakeWhile(q => q.Id != "park_plan")) staffed.Flags.Add("tutorial:" + q.Id);
            Check(staffed.HasPark && staffed.CurrentQuest()?.Id == "park_plan", "已进入园区经营阶段才出现园区说明");
            _game.Ui.OpenQuestWindow();
            CheckGuideReadOnly();
            Shot("vs1_guide_park");
            ClickGuideButton("知道了");
            Check(!staffed.QuestCompleted("park_plan") && staffed.CurrentQuest()?.Id == "park_plan", "关闭说明不会跳过或自动完成任务");
            ClickGuideButton("经营");
            ClickGuideButton("› 园区布局");
            Check(GuideButton("已了解，完成这一步") != null, "园区页显示预算教学确认");
            ClickGuideButton("已了解，完成这一步");
            Check(staffed.QuestCompleted("park_plan"), "当前教学完成后自然切换");
            _game.Ui.CloseAllWindows();
            Check(!staffed.Time.IsHeld, "多次打开指南没有残留暂停");
            foreach (var q in Quests.All) staffed.Flags.Add("tutorial:" + q.Id);
            _game.Ui.DebugRefreshQuests();
            _game.Ui.OpenQuestWindow();
            Check(_game.Ui.QuestTrackerText == null && !_game.Ui.AnyModalOpen, "任务结束后隐藏提示，没有回看目录");

        }

        private static void CheckGuideReadOnly()
        {
            var window = GameObject.Find("Window_当前任务");
            Check(window != null, "当前任务说明已打开");
            if (window == null) return;
            var labels = window.GetComponentsInChildren<UnityEngine.UI.Button>()
                .Select(b => b.GetComponentInChildren<UnityEngine.UI.Text>()?.text).ToArray();
            Check(labels.Length == 2 && labels.All(t => t == "关闭" || t == "知道了"),
                "教学窗口只含关闭和知道了，不含收起任务条、出行或页面跳转按钮");
        }

        private static UnityEngine.UI.Button AgencyButton(string name) => _game.Ui.Canvases
            .SelectMany(c => c.GetComponentsInChildren<UnityEngine.UI.Button>())
            .LastOrDefault(b => b.gameObject.activeInHierarchy && b.name == name);

        private static void ClickAgency(string name)
        {
            var button = AgencyButton(name);
            Check(button != null && button.interactable, "选址图按钮可用：" + name);
            if (button != null)
            {
                var scroll = button.GetComponentInParent<UnityEngine.UI.ScrollRect>();
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, button.transform);
                Rect view = scroll.viewport.rect;
                Check(bounds.min.x >= view.xMin - 1 && bounds.max.x <= view.xMax + 1 && bounds.min.y >= view.yMin - 1 && bounds.max.y <= view.yMax + 1,
                    "选址按钮完整显示在可点击区域：" + name);
            }
            if (button != null && button.interactable) button.onClick.Invoke();
            Canvas.ForceUpdateCanvases();
        }

        private static void SmokeAgencySelection(GameSession s)
        {
            Place agency = s.CurrentPlace;
            Money balance = s.Ledger.Balance;
            foreach (var unit in s.City.Places.Where(p => p.Kind == PlaceKind.Shop || p.Kind == PlaceKind.Agency))
                Check(AgencyButton("AgencyLot_" + unit.Id) != null, "片区包含沿街地点：" + unit.Id);
            foreach (var listing in s.Listings)
                Check(AgencyButton("AgencyPin_" + listing.PlaceId) != null, "挂牌铺面有价格客流牌：" + listing.PlaceId);
            var future = s.Listings.First(l => l.OpenDay > s.Clock.Today.TotalDays);
            ClickAgency("AgencyLot_" + future.PlaceId);
            Check(AgencyButton("AgencyReviewLease")?.interactable == true, "未到期铺面也可以提前签");
            Shot("vs1_agency_future");
            var unlisted = s.City.Places.First(p => p.Kind == PlaceKind.Shop && !s.Listings.Any(l => l.PlaceId == p.Id));
            ClickAgency("AgencyLot_" + unlisted.Id);
            Check(AgencyButton("AgencyReviewLease") == null, "未挂牌铺面没有签约按钮");
            ClickAgency("AgencyPin_player_shop");
            ClickAgency("AgencyReviewLease");
            Check(!s.HasShop && s.Ledger.Balance == balance, "核对费用前不扣款、不租下");
            Shot("vs1_agency_confirm");
            ClickGuideButton("再看看");
            Check(!s.HasShop && s.Ledger.Balance == balance && s.CurrentPlace == agency && s.Trip == null,
                "点选、核价、取消始终留在中介且不扣款");
            Shot("vs1_agency_map");
            SmokeAgencyMapControls(s);
        }

        private static void SmokeAgencyMapControls(GameSession s)
        {
            Vector3 cameraPosition = _game.Cam.transform.position;
            Quaternion rotation = _game.Cam.transform.rotation;
            float size = _game.Cam.orthographicSize;
            ClickAgency("AgencyFocusUnit");
            var map = UnityEngine.Object.FindObjectOfType<PCShopSell.Game.UI.AgencyStreetMap>();
            Check(map != null && map.Zoom > 2, "可以放大选中建筑查看周围关系");
            Shot("vs1_agency_birdseye_close");
            var eventData = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
            { position = new Vector2(700, 500), button = UnityEngine.EventSystems.PointerEventData.InputButton.Left };
            eventData.pointerPressRaycast = new UnityEngine.EventSystems.RaycastResult
            { module = map.GetComponentInParent<UnityEngine.UI.GraphicRaycaster>(), gameObject = map.gameObject };
            map.OnBeginDrag(eventData);
            eventData.position += new Vector2(60, 30);
            map.OnDrag(eventData);
            map.OnEndDrag(eventData);
            eventData.scrollDelta = new Vector2(0, 1);
            float previousZoom = map.Zoom;
            map.OnScroll(eventData);
            Check(map.Zoom > previousZoom, "鸟瞰图滚轮缩放生效");
            ClickAgency("AgencyResetView");
            map = UnityEngine.Object.FindObjectOfType<PCShopSell.Game.UI.AgencyStreetMap>();
            Check(map.Zoom == 1, "一键恢复完整街区视图");
            var renderer = _game.World.Places["player_shop"].Root.transform.Find("Body").GetComponent<Renderer>();
            Vector3 roof = renderer.bounds.center; roof.y = renderer.bounds.max.y + .01f;
            Vector2 point = map.Project(roof);
            Check(map.PlaceAt(new Vector2(point.x / map.Width, 1 - point.y / map.Height)) == "player_shop", "通过真实三维建筑射线识别目标铺面");
            var canvas = map.GetComponentInParent<Canvas>();
            eventData.position = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, map.transform.TransformPoint(new Vector3(point.x, -point.y, 0)));
            map.OnPointerDown(eventData);
            map.OnPointerClick(eventData);
            Check(AgencyButton("AgencyReviewLease")?.interactable == true, "直接点三维建筑可以选房，拖动结束后仍可点击");
            Check(_game.Cam.transform.position == cameraPosition && _game.Cam.transform.rotation == rotation && _game.Cam.orthographicSize == size,
                "查看鸟瞰图不改变主场景镜头");
            Check(s.InAgency && s.Trip == null, "缩放拖动和点楼都不触发人物出行");
            Check(UnityEngine.Object.FindObjectsOfType<Camera>().Count(c => c.name == "AgencyStreetCamera") == 1, "重建选房页只保留一个预览相机");
        }

        private static void SmokeAgency()
        {
            _outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../docs/previews"));
            Directory.CreateDirectory(_outDir);
            ShaderUtil.allowAsyncCompilation = false;
            UrpSetup.Ensure();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Camera cam = BuildRig();
            _game = new GameObject("Game").AddComponent<GameController>();
            _game.Bootstrap(false, 7);
            _game.Ui.QuestPopups = false;
            foreach (Canvas c in _game.Ui.Canvases)
            {
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = 1f;
            }
            Canvas.ForceUpdateCanvases();
            _game.Step(0);
            var s = _game.Session;
            Teleport(s, "agency");
            _game.Ui.OnEnteredPlace(s.CurrentPlace);
            SmokeAgencySelection(s);
            Shot("vs1_agency_map_720", 1280, 720);
            Check(GuideButton("和小陈聊聊") == null, "中介窗口没有重复的“和小陈聊聊”按钮，点头像交谈");
            var portrait = _game.Ui.Canvases.SelectMany(c => c.GetComponentsInChildren<UnityEngine.UI.Button>())
                .LastOrDefault(b => b.gameObject.activeInHierarchy && b.name == "AgentPortrait");
            Check(portrait != null, "中介窗口有可点的小陈头像");
            portrait?.onClick.Invoke();
            Canvas.ForceUpdateCanvases();
            ClickGuideButton("随便聊聊（8 分钟）");
            ClickGuideButton("继续看房");
            Check(s.AgentDiscount > 0, "地图中的中介交谈保留熟络优惠");
            Money cost = s.LeaseCost(s.Listings.First(l => l.PlaceId == "player_shop"));
            Money before = s.Ledger.Balance;
            ClickAgency("AgencyReviewLease");
            ClickAgency("AgencyConfirmLease");
            Check(s.HasShop && s.PlayerShopPlace.Id == "player_shop" && s.Ledger.Balance == before - cost, "确认签约按折后报价扣款并租下选中铺面");
            Check(s.InAgency && s.Trip == null, "签约后仍在中介，不自动出行");
            Check(AgencyButton("AgencyPin_player_shop") == null && AgencyButton("AgencyReviewLease") == null, "签下的店变为我的主店、不能重复签约");
            _game.Ui.CloseAllWindows();
            Teleport(s, "agency");
            s.DebugAddListing("shop02", s.Clock.Today.TotalDays, s.Clock.Today.TotalDays + 30, Money.FromYuan(6200));
            _game.Ui.OnEnteredPlace(s.CurrentPlace);
            ClickAgency("AgencyLot_shop02");
            Check(AgencyButton("AgencyReviewLease")?.interactable == false, "名气不足不能搬到中型店");
            s.PlayerShop.Reputation = 100;
            _game.Ui.RebuildWindows();
            Check(AgencyButton("AgencyReviewLease")?.interactable == false, "资金不足不能签约");
            Shot("vs1_agency_insufficient");
            s.Ledger.Credit(s.Clock.Today, "验收", Money.FromYuan(100000), "选址验收资金");
            _game.Ui.RebuildWindows();
            Shot("vs1_agency_move");
            var next = s.Listings.First(l => l.PlaceId == "shop02");
            before = s.Ledger.Balance;
            cost = s.LeaseCost(next);
            ClickAgency("AgencyReviewLease");
            Shot("vs1_agency_move_confirm");
            ClickAgency("AgencyConfirmLease");
            Check(s.PlayerShopPlace.Id == "shop02" && s.Ledger.Balance == before - cost, "搬店报价正确结算旧押金和搬家费");
            Check(s.InAgency && s.Trip == null, "搬店签约不移动人物");

            _game.Ui.CloseAllWindows();
            var dense = s.CreateSave();
            dense.listings = new[] { "shop01", "shop05", "shop16", "shop08", "shop09", "shop10" }
                .Select((id, i) => new SaveListing { place = id, size = (int)s.City.GetPlace(id).Size,
                    rentCents = Money.FromYuan(30000 - i * 4000).Cents,
                    openDay = s.Clock.Today.TotalDays + (i % 2 == 0 ? 0 : 15), closeDay = s.Clock.Today.TotalDays + 45 }).ToArray();
            File.WriteAllText(SaveStore.PathOf, JsonUtility.ToJson(dense));
            _game.LoadGame();
            s = _game.Session;
            Teleport(s, "agency");
            _game.Ui.OnEnteredPlace(s.CurrentPlace);
            Canvas.ForceUpdateCanvases();
            var pins = s.Listings.Select(l => (RectTransform)AgencyButton("AgencyPin_" + l.PlaceId).transform)
                .OrderBy(rt => rt.anchoredPosition.x).ToArray();
            for (int i = 0; i < pins.Length; i++)
            {
                var box = new Rect(pins[i].anchoredPosition.x, -pins[i].anchoredPosition.y, pins[i].rect.width, pins[i].rect.height);
                Check(box.xMin >= 0 && box.xMax <= 1120 && box.yMin >= 0 && box.yMax <= 650, "鸟瞰图六房源报价牌不越界");
                for (int j = 0; j < i; j++)
                {
                    var other = new Rect(pins[j].anchoredPosition.x, -pins[j].anchoredPosition.y, pins[j].rect.width, pins[j].rect.height);
                    Check(!box.Overlaps(other), "鸟瞰图相邻报价牌不重叠");
                }
            }
            ClickAgency("AgencyPin_shop01");
            Shot("vs1_agency_dense");
            before = s.Ledger.Balance;
            ClickAgency("AgencyReviewLease");
            s.Listings.First(l => l.PlaceId == "shop01").Rent += Money.FromYuan(100);
            ClickAgency("AgencyConfirmLease");
            Check(s.Ledger.Balance == before && s.PlayerShopPlace.Id == "shop02", "报价变化后必须重新核对，不按旧报价扣款");
            ClickAgency("AgencyReviewLease");
            ClickAgency("AgencyConfirmLease");
            Check(s.PlayerShopPlace.Id == "shop01", "重新核价后可正常租下大型铺面");
            _game.Ui.CloseAllWindows();
            Teleport(s, "agency");
            var cheaper = s.DebugAddListing("shop05", s.Clock.Today.TotalDays, s.Clock.Today.TotalDays + 30, Money.FromYuan(1500));
            _game.Ui.OnEnteredPlace(s.CurrentPlace);
            ClickAgency("AgencyLot_shop05");
            before = s.Ledger.Balance;
            cost = s.LeaseCost(cheaper);
            Check(cost.IsNegative, "大铺换小铺，旧押金抵扣后产生退款");
            Shot("vs1_agency_refund");
            ClickAgency("AgencyReviewLease");
            ClickAgency("AgencyConfirmLease");
            Check(s.PlayerShopPlace.Id == "shop05" && s.Ledger.Balance == before - cost, "搬店净退款正确入账");
            _game.Ui.CloseAllWindows();
            Check(!UnityEngine.Object.FindObjectsOfType<Camera>().Any(c => c.name == "AgencyStreetCamera"), "关闭中介释放三维预览相机");
            Teleport(s, "agency");
            s.Clock.Restore(s.Clock.Today, 22 * 60);
            Frame(.05f);
            Color ambient = RenderSettings.ambientLight;
            bool fog = RenderSettings.fog;
            _game.Ui.OnEnteredPlace(s.CurrentPlace);
            Check(RenderSettings.ambientLight == ambient && RenderSettings.fog == fog, "鸟瞰图白天采光不污染主场景夜间环境");
            Shot("vs1_agency_birdseye_night");
        }

        private static UnityEngine.UI.Button GuideButton(string text) => _game.Ui.Canvases
            .SelectMany(c => c.GetComponentsInChildren<UnityEngine.UI.Button>())
            .LastOrDefault(b => b.gameObject.activeInHierarchy && b.GetComponentInChildren<UnityEngine.UI.Text>()?.text == text);

        private static void ClickGuideButton(string text)
        {
            var b = GuideButton(text);
            Check(b != null && b.interactable, "可点击引导按钮：" + text);
            if (b != null && b.interactable) b.onClick.Invoke();
            Canvas.ForceUpdateCanvases();
        }

        private static void Teleport(GameSession s, string placeId)
        {
            Place p = s.City.GetPlace(placeId);
            _game.Ui.CloseAllWindows();
            s.StopTrip();
            s.Leave();
            s.ArriveAtNode(p.NodeId);
            s.Enter(p);
            Feed(s);
        }

        /// <summary>冒烟一路快进好几天、不做饭：直接喂饱，免得饿进医院打乱流程。</summary>
        private static void Feed(GameSession s)
        {
            s.Player.Hunger = 100;
            s.Player.Health = 100;
        }

        private static void Run(Func<bool> until, int maxFrames)
        {
            for (int i = 0; i < maxFrames && !until(); i++) Frame(0.05f);
        }

        /// <summary>推进一帧；途中弹出的提示窗直接关掉（否则会暂停时间）。</summary>
        private static void Frame(float dt)
        {
            if (_game.Ui.AnyModalOpen && _game.Session.Trip != null)
                _game.Ui.CloseModalWindows();
            _game.Step(dt);
        }

        private static void Check(bool ok, string what)
        {
            if (ok) Log("OK " + what);
            else Debug.LogError("SMOKE FAIL: " + what);
        }

        private static void Log(string m) => Debug.Log("SMOKE " + m);

        private static void SmokeHomeMotion()
        {
            var motion = UnityEngine.Object.FindObjectsOfType<PCShopSell.Game.Runtime.FigureMotion>()
                .First(m => m.name == "Owner" && Vector3.Distance(m.transform.position, _game.HomeRoom.OwnerWorld) < .01f);
            const float dt = 1f / 30f;
            Directory.CreateDirectory(Path.Combine(_outDir, "motion"));
            bool walked = false;
            for (int i = 0; i < 84; i++)
            {
                Vector3 dir = i < 28 ? Vector3.left : i >= 44 && i < 72 ? Vector3.right : Vector3.zero;
                if (dir != Vector3.zero) _game.HomeRoom.MoveOwner(dir, dt);
                _game.Step(0); // Exercise real room movement and animation without advancing the smoke fixture's clock.
                PCShopSell.Game.Runtime.FigureMotion.AdvanceAll(dt, true);
                walked |= motion.WalkBlend > .8f;
                Shot("motion/home_" + i.ToString("D3"), 960, 540);
            }
            Check(walked, "出租屋移动带动双腿和摆臂");
            Check(motion.WalkBlend < .01f, "松开移动键后自然收步");
            for (int i = 0; i < 60; i++)
            {
                _game.HomeRoom.MoveOwner(Vector3.right, dt);
                _game.Step(0);
                PCShopSell.Game.Runtime.FigureMotion.AdvanceAll(dt, true);
            }
            Check(motion.WalkBlend < .01f, "被墙挡住时不原地踏步");
            _game.HomeRoom.ResetOwner();
            _game.Step(0);
        }

        private static void Shot(string name, int w = 1920, int h = 1080)
        {
            Camera cam = _game.Cam;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            _game.Step(0.0f);
            Canvas.ForceUpdateCanvases();
            cam.Render();
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(Path.Combine(_outDir, name + ".png"), tex.EncodeToPNG());
            cam.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(tex);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            Log("shot " + name);
        }
    }
}
