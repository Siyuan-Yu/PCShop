using System;
using System.IO;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;
using PCShopSell.Game;
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

        public static void SmokeFromBatch()
        {
            int errors = 0;
            Application.LogCallback onLog = (msg, stack, type) =>
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
            };
            Application.logMessageReceived += onLog;
            int code = 0;
            try
            {
                Smoke();
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
            _game.Bootstrap(false);
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
            Frame(0.05f);
            Shot("vs1_83_intro_no_shop");
            Teleport(s, "agency");
            _game.Ui.OnEnteredPlace(s.CurrentPlace);
            Frame(0.05f);
            Shot("vs1_84_agency");
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
            Check(walkRate > idleRate * 0.3 && walkRate < idleRate * 0.8, "走路时钟速度约为站着的一半（画面速度已减半，每公里耗时不变）");
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
            for (int i = 0; i < 20 && !_game.Interior.OwnerOutside; i++) _game.Interior.MoveOwner(UnityEngine.Vector3.back, 0.1f);
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
                    _game.Checkout(s.PlayerShop.Queue[0].Id);
                    sold++;
                }
            }
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
            _game.DebugOrbit(-40f, 10f);
            for (int i = 0; i < 40; i++) Frame(0.05f);
            Shot("vs1_31_shop_decor");
            _game.EndPlacement();
            _game.Ui.CloseAllWindows();
            Check(!s.PlayerShop.IsOpen, "装修当天店歇业");
            // 存档 / 读档
            Teleport(s, "home");
            SaveStore.Save(s);
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
            Check((_game.Ui.QuestTrackerText ?? "").Contains("家具城"), "任务条：租好后去家具城买设施：" + _game.Ui.QuestTrackerText);
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
            Check((_game.Ui.QuestTrackerText ?? "").Contains("摆好"), "任务条：买齐了就去摆好：" + _game.Ui.QuestTrackerText);
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
            Check((_game.Ui.QuestTrackerText ?? "").Contains("设置"), "任务条：摆好后去设置货架：" + _game.Ui.QuestTrackerText);
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

        private static void Shot(string name)
        {
            const int w = 1920, h = 1080;
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
