using System;
using System.IO;
using PCShopSell.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PCShopSell.EditorTools
{
    /// <summary>
    /// 真正进 Play 模式跑 Main.unity：走你验收时会走的路径（Start → Bootstrap → Update 循环 → 进店 → 加速）。
    /// 命令：Unity -batchmode -executeMethod PCShopSell.EditorTools.PlayModeSmoke.RunFromBatch（不能带 -nographics）。
    /// 进 Play 会重载域，所以用 SessionState 保存状态、用 InitializeOnLoad 重新挂回调。
    /// </summary>
    [InitializeOnLoad]
    public static class PlayModeSmoke
    {
        private const string ActiveKey = "PCShopSell.PlaySmoke.Active";
        private const string ErrorsKey = "PCShopSell.PlaySmoke.Errors";
        private const string StartKey = "PCShopSell.PlaySmoke.Start";

        private static double _t0;
        private static int _frame;
        private static bool _enteredShop;
        private static int _shots;

        static PlayModeSmoke()
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            Application.logMessageReceived += OnLog;
            EditorApplication.update += Tick;
        }

        public static void RunFromBatch()
        {
            EditorSceneManager.OpenScene(GameSceneBuilder.ScenePath, OpenSceneMode.Single);
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetInt(ErrorsKey, 0);
            SessionState.SetFloat(StartKey, 0);
            EditorApplication.EnterPlaymode();
        }

        private static void OnLog(string msg, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                SessionState.SetInt(ErrorsKey, SessionState.GetInt(ErrorsKey, 0) + 1);
                Debug.Log("PLAYSMOKE error: " + msg);
            }
        }

        private static void Finish(int code)
        {
            Debug.Log($"PLAYSMOKE RESULT code={code} errors={SessionState.GetInt(ErrorsKey, 0)} frames={_frame}");
            SessionState.SetBool(ActiveKey, false);
            EditorApplication.Exit(code);
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            if (_t0 == 0) _t0 = EditorApplication.timeSinceStartup;
            _frame++;
            double elapsed = EditorApplication.timeSinceStartup - _t0;

            var game = UnityEngine.Object.FindObjectOfType<GameController>();
            if (game == null)
            {
                if (elapsed > 5) { Debug.LogError("PLAYSMOKE: no GameController in scene"); Finish(1); }
                return;
            }
            if (game.Session == null || game.Ui == null) return;

            try
            {
                if (!_enteredShop && elapsed > 3)
                {
                    _enteredShop = true;
                    game.Ui.CloseAllWindows();
                    Debug.Log("PLAYSMOKE: boot ok, places=" + game.City.Places.Count + ", walking to shop");
                    Capture("play_01_start");
                    game.Session.Time.SetSpeed(8);
                    game.GoToPlace(game.City.PlayerShop());
                }
                if (_enteredShop && _shots == 1 && game.InInterior)
                {
                    Capture("play_02_in_shop");
                }
                if (elapsed > 40 || (_enteredShop && game.InInterior && elapsed > 25))
                {
                    Debug.Log($"PLAYSMOKE: day={game.Session.Clock.Now.Date.TotalDays} minute={game.Session.Clock.MinuteOfDay} inShop={game.InInterior} cash={game.Session.Ledger.Balance}");
                    Finish(SessionState.GetInt(ErrorsKey, 0) > 0 ? 1 : 0);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Finish(1);
            }
        }

        private static void Capture(string name)
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../docs/previews"));
            Directory.CreateDirectory(dir);
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, name + ".png"));
            _shots++;
        }
    }
}
