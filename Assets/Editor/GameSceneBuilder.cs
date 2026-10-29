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
            Shot("vs1_13_home");
            _game.Ui.CloseAllWindows();

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
