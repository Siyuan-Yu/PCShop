using System;
using System.IO;
using System.Linq;
using PCShopSell.Core.Catalog;
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
            _game.Ui.CloseAllWindows();
            s.Leave();
            Shot("vs1_01_home_morning");

            _game.GoToPlace(s.PlayerShopPlace);
            Run(() => s.Trip == null, 600);
            Check(s.InPlayerShop, "走到店里并进店");
            Log("到店 " + s.Clock.Now);

            s.SetMarkup(1.1);
            for (int i = 0; i < 400 && s.PlayerShop.Queue.Count == 0; i++) Frame(0.1f);
            Shot("vs1_02_shop_interior");
            int sold = 0;
            for (int i = 0; i < 1600; i++)
            {
                _game.Ui.CloseModalWindows();
                Frame(0.1f);
                if (s.PlayerShop.Queue.Count > 0)
                {
                    _game.Checkout(s.PlayerShop.Queue[0].Id);
                    sold++;
                }
            }
            Log($"卖出 {sold} 单，现金 {s.Ledger.Balance}，时间 {s.Clock.Now}");
            Check(sold > 0, "有顾客结账");

            _game.Ui.RebuildDock();
            Shot("vs1_03_shop_counter");

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
            Check(s.CompleteBuild(orderField.Id, 1), "交付装机订单");

            // 新内容：升级面板 / 随机事件窗口 / 家里的衣橱
            Teleport(s, "player_shop");
            _game.Ui.OnEnteredPlace(s.CurrentPlace);
            _game.Ui.DebugSelectTab(_game.Ui.DebugTabCount - 2);
            Frame(0.05f);
            Shot("vs1_11_upgrades");
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
            _game.HomeInteract(PCShopSell.Game.Runtime.HomeRoomView.Wardrobe);
            Frame(0.05f);
            Check(_game.Ui.AnyModalOpen, "衣柜窗口打开");
            Shot("vs1_22_wardrobe");
            _game.Ui.CloseAllWindows();
            _game.HomeInteract(PCShopSell.Game.Runtime.HomeRoomView.Bed);
            Frame(0.05f);
            Shot("vs1_23_bed");
            _game.Ui.CloseAllWindows();

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
            _game.Ui.CloseAllWindows();

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
            Teleport(s, "shop12");
            _game.Ui.OnEnteredPlace(s.CurrentPlace);
            Frame(0.05f);
            Shot("vs1_28_mall");
            foreach (string id in new[] { "bed_spring", "aircon", "sofa", "shop_sofa", "shop_led" })
            {
                Check(!s.BuyFurnitureAtMall(id), "太大件背包装不下 " + id);
                Check(s.BuyFurnitureByTruck(id), "叫货车拖 " + id);
            }
            foreach (string id in new[] { "curtain", "plant", "rug", "poster", "shop_plant", "shop_poster", "shop_dispenser" })
                Check(s.BuyFurnitureAtMall(id), "家具城买 " + id);
            _game.Ui.CloseAllWindows();
            Teleport(s, "shop14");
            _game.Ui.OnEnteredPlace(s.CurrentPlace);
            Frame(0.05f);
            Shot("vs1_29_farm");
            Check(s.BuyIngredient("egg", 2), "农贸市场买鸡蛋");
            Check(s.BuyGift("fruit"), "农贸市场买水果");
            _game.Ui.CloseAllWindows();

            // 烟酒茶行、手机店、背包
            Teleport(s, "shop19");
            _game.Ui.OnEnteredPlace(s.CurrentPlace);
            Frame(0.05f);
            Shot("vs1_32_tobacco");
            Check(s.BuyGift("cig"), "烟酒茶行买烟");
            Check(s.BuyGift("wine"), "烟酒茶行买酒");
            _game.Ui.CloseAllWindows();
            s.SetSmartphone(false);
            Teleport(s, "shop04");
            _game.Ui.OnEnteredPlace(s.CurrentPlace);
            Frame(0.05f);
            Shot("vs1_33_phone_shop");
            Check(s.BuySmartphone(), "手机店买智能手机");
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
            Frame(0.05f);
            Shot("vs1_30_placing");
            var home = PCShopSell.Core.Home.RoomSpec.Home;
            Check(s.PlaceFromStock(false, "bed_spring", PCShopSell.Core.Home.RoomPlanner.Snap(home, PCShopSell.Core.Home.FurnitureCatalog.Get("bed_spring"), 1.05, 4.0, 0)), "摆弹簧床");
            Check(s.PlaceFromStock(false, "aircon", PCShopSell.Core.Home.RoomPlanner.Snap(home, PCShopSell.Core.Home.FurnitureCatalog.Get("aircon"), 1.25, 0, 0)), "挂空调");
            Check(s.PlaceFromStock(false, "curtain", PCShopSell.Core.Home.RoomPlanner.Snap(home, PCShopSell.Core.Home.FurnitureCatalog.Get("curtain"), 3.15, 0, 0)), "挂窗帘");
            Check(s.PlaceFromStock(false, "sofa", PCShopSell.Core.Home.RoomPlanner.Snap(home, PCShopSell.Core.Home.FurnitureCatalog.Get("sofa"), 2.75, 1.0, 0)), "摆沙发");
            Check(s.PlaceFromStock(false, "rug", PCShopSell.Core.Home.RoomPlanner.Snap(home, PCShopSell.Core.Home.FurnitureCatalog.Get("rug"), 2.75, 2.7, 0)), "铺地毯");
            _game.EndPlacement();
            for (int i = 0; i < 10; i++) Frame(0.05f);
            Check(_game.Ui != null && !_game.Placing, "摆放结束");
            Shot("vs1_24_home_furnished");
            _game.Ui.CloseAllWindows();

            // 店里摆设：歇业装修一天
            Teleport(s, "player_shop");
            _game.Ui.OnEnteredPlace(s.CurrentPlace);
            Check(s.BeginRenovation(), "歇业装修");
            Check(_game.BeginPlacement(true), "店里进入摆放模式");
            var shopRoom = s.ShopRoom;
            Check(s.PlaceFromStock(true, "shop_sofa", PCShopSell.Core.Home.RoomPlanner.Snap(shopRoom, PCShopSell.Core.Home.FurnitureCatalog.Get("shop_sofa"), 4.5, 1.0, 0), _game.Interior.FixedBlockers()), "摆等候沙发");
            Check(s.PlaceFromStock(true, "shop_plant", PCShopSell.Core.Home.RoomPlanner.Snap(shopRoom, PCShopSell.Core.Home.FurnitureCatalog.Get("shop_plant"), 6.4, 1.0, 0), _game.Interior.FixedBlockers()), "摆绿植");
            Check(s.PlaceFromStock(true, "shop_led", PCShopSell.Core.Home.RoomPlanner.Snap(shopRoom, PCShopSell.Core.Home.FurnitureCatalog.Get("shop_led"), 4.0, 0, 0)), "挂 LED");
            for (int i = 0; i < 10; i++) Frame(0.05f);
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

            // 店面扩建 + 目标与人脉面板
            g.Ledger.Credit(g.Clock.Today, "测试", PCShopSell.Core.Common.Money.FromYuan(500000), "演示用");
            g.PlayerShop.Reputation = 50;
            Teleport(g, "player_shop");
            g.Clock.Restore(new PCShopSell.Core.Time.GameDate(130), 10 * 60);
            Check(g.BuyExpansion(PCShopSell.Core.Shop.ShopSize.Medium), "扩建成中型店");
            _game.Ui.CloseModalWindows();
            Frame(0.05f);
            Shot("vs1_14_medium_shop");
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
            g.AddRelation(PCShopSell.Core.Session.Npcs.Wang, 45);
            g.AskForTip(PCShopSell.Core.Session.Npcs.Wang);
            _game.Ui.CloseModalWindows();
            _game.Ui.OnEnteredPlace(g.CurrentPlace);
            Frame(0.05f);
            Shot("vs1_16_stall_owner");
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
