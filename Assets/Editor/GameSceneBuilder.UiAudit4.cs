using System;
using System.IO;
using System.Linq;
using System.Reflection;
using PCShopSell.Core.Session;
using PCShopSell.Game;
using PCShopSell.Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PCShopSell.EditorTools
{
    public static partial class GameSceneBuilder
    {
        /// <summary>补拍：上一轮因前置条件静默失败而未打开的窗口。</summary>
        public static void UiAudit4FromBatch() => RunSmokeBatch(() =>
        {
            _outDir = Path.GetFullPath("docs/previews/ui-audit4"); Directory.CreateDirectory(_outDir);
            ShaderUtil.allowAsyncCompilation = false; UrpSetup.Ensure(); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); BuildRig();
            _game = new GameObject("Game").AddComponent<GameController>(); _game.Bootstrap(false, 11); _game.Ui.QuestPopups = false;
            var s = _game.Session; s.EventsEnabled = false; s.Time.SetPaused(true);
            s.DebugAddCash(3000000); s.SetSmartphone(true);
            foreach (var canvas in _game.Ui.Canvases) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = _game.Cam; canvas.planeDistance = 1; }
            Log("STARTER " + s.DebugRentStarterShop(true));
            for (int i = 0; i < 3; i++) s.AdvanceClock(24 * 60);
            var ui = _game.Ui; var t = typeof(GameUi);
            void Snap(string name) { Frame(0); _game.SnapCamera(); Shot(name, 1920, 1080); Shot(name, 1920, 1080); }
            void Call(string method)
            {
                var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                var m = t.GetMethod(method, flags, null, Type.EmptyTypes, null);
                object[] args = null;
                if (m == null)
                {
                    m = t.GetMethods(flags).FirstOrDefault(x => x.Name == method && x.GetParameters().All(p => p.IsOptional));
                    if (m != null) args = new object[m.GetParameters().Length];
                }
                if (m == null) { Log("NO METHOD " + method); return; }
                ui.CloseAllWindows();
                try { m.Invoke(ui, args); Frame(0.05f); }
                catch (Exception e) { Log("FAIL " + method + " :: " + (e.InnerException ?? e).Message); }
            }

            // 先进入自己的店内，再拍依赖 InPlayerShop 的窗口
            var shop = s.MyShops.FirstOrDefault();
            if (shop != null) { Teleport(s, shop.Place.Id); Frame(0.1f); Log("ENTERED SHOP inPlayerShop=" + s.InPlayerShop); }
            Call("OpenShopBuyers"); Snap("71-buyers");
            Call("OpenPetOrders"); Snap("72-pets");
            Call("OpenDock"); Snap("73-dock");
            s.Leave(); Frame(0.1f);

            // 居家工作台需要大屏
            s.DebugSetHomeWorkbench(true); Frame(0.1f);
            Log("workbench usable=" + s.CanUseHomeWorkbench);
            Call("OpenHomeWorkbench"); Snap("74-workbench");
            ui.CloseAllWindows();

            // 顾客与进货员列表在店外会静默失败；顺便验证提示缺失
            s.Leave(); Frame(0.1f);
            Call("OpenShopBuyers"); Snap("75-buyers-outside");

            Log("UI AUDIT 4 COMPLETE");
        });
    }
}
