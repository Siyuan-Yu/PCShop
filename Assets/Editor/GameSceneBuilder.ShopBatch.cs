using System;
using System.IO;
using System.Linq;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PCShopSell.EditorTools
{
    public static partial class GameSceneBuilder
    {
        public static void ShopBatchFromBatch() => RunSmokeBatch(() =>
        {
            _outDir = Path.GetFullPath("docs/previews/shop81-90"); Directory.CreateDirectory(_outDir);
            ShaderUtil.allowAsyncCompilation = false; UrpSetup.Ensure(); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = BuildRig(); _game = new GameObject("Game").AddComponent<GameController>(); _game.Bootstrap(false, 1234); _game.StartCityLifePreset("solo"); _game.Ui.QuestPopups = false;
            foreach (var c in _game.Ui.Canvases) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = camera; c.planeDistance = 1; }
            var s = _game.Session; s.EventsEnabled = false; s.MainShop.Sim.NotFitted = false; s.MainShop.Sim.ForcedClosed = false; s.MainShop.Sim.SetHours(0, 1440); s.MainShop.Sim.Settings.BaseCustomersPerHour = 0;
            s.Player.Health = s.Player.Hunger = s.Player.Energy = 100; _game.Ui.CloseAllWindows(); Teleport(s, s.MainShop.Place.Id); Frame(0); _game.SnapCamera();
            s.RaiseEvent("shop_cat_visitor"); if (s.PendingEvent == null) throw new InvalidOperationException("Cat event unavailable"); s.ResolveEvent(0); _game.Ui.CloseAllWindows(); Frame(0);
            if (GameObject.Find("TemporaryShopCat") == null || GameObject.Find("WaterBowl") == null) throw new InvalidOperationException("Actual cat and water bowl missing"); Shot("83-cat-and-water");
            s.Meet("zhou", 40); s.RaiseEvent("shop_supplier_gift"); if (s.PendingEvent == null) throw new InvalidOperationException("Sample event unavailable"); s.ResolveEvent(0);
            var sample = s.ShopEventTasks.Single(t => t.Kind == "sample"); s.Clock.Restore(new GameDate((int)(sample.At / 1440)), (int)(sample.At % 1440) - 1); s.AdvanceClock(1); _game.Ui.CloseAllWindows(); Frame(0);
            if (GameObject.Find("BorrowedSupplierSample") == null) throw new InvalidOperationException("Actual loan sample prop missing"); Shot("84-physical-loan-sample");
            _game.Ui.OpenSchedulePanel(); Shot("81-90-schedule-entry"); _game.Ui.CloseAllWindows(); _game.Ui.OpenShopEventTasks(); Shot("84-sample-actions"); _game.Ui.CloseAllWindows();
            if (!s.StartShopEventTask(sample.Id)) throw new InvalidOperationException("Sample trial could not start"); s.AdvanceClock(10);
            if (s.ShopEventTasks.Single(t => t.Id == sample.Id).Status != "已试用" || !s.FinishSupplierSample(sample.Id, false)) throw new InvalidOperationException("Real sample trial/return failed");
            s.RaiseEvent("shop_inventory_audit"); if (s.PendingEvent == null) throw new InvalidOperationException("Audit unavailable"); s.ResolveEvent(0);
            var audit = s.ShopEventTasks.Single(t => t.Kind == "audit"); if (!s.StartShopEventTask(audit.Id)) throw new InvalidOperationException("Actual audit could not start"); s.AdvanceClock(30);
            if (s.ShopEventTasks.Any(t => t.Id == audit.Id)) throw new InvalidOperationException("Actual audit did not finish");
            File.WriteAllText(Path.Combine(_outDir, "runtime-results.txt"), "Production controller: temporary cat and water bowl; visible borrowed sample; real next-day delivery, ten-minute trial and return; calendar task entry and live actions; real thirty-minute onsite audit.\n");
            Log("Shop 81-90 physical cat, sample, actual trial and audit passed.");
        });
    }
}
