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
        public static void FinanceBatchFromBatch() => RunSmokeBatch(() =>
        {
            string events = "Assets/Resources/Data/events.json";
            File.WriteAllText(events, EventScriptWriter.Write(EventScriptReader.Read(File.ReadAllText(events))));
            _outDir = Path.GetFullPath("docs/previews/finance91-100"); Directory.CreateDirectory(_outDir);
            ShaderUtil.allowAsyncCompilation = false; UrpSetup.Ensure(); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = BuildRig(); _game = new GameObject("Game").AddComponent<GameController>(); _game.Bootstrap(false, 1234); _game.StartCityLifePreset("solo"); _game.Ui.QuestPopups = false;
            foreach (var c in _game.Ui.Canvases) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = camera; c.planeDistance = 1; }
            var s = _game.Session; s.EventsEnabled = false; s.SetSmartphone(true); s.MainShop.Sim.NotFitted = false; s.MainShop.Sim.ForcedClosed = false; s.MainShop.Sim.SetHours(0, 1440); s.MainShop.Sim.Settings.BaseCustomersPerHour = 0;
            s.Player.Health = s.Player.Hunger = s.Player.Energy = 100; _game.Ui.CloseAllWindows(); Teleport(s, s.MainShop.Place.Id); Frame(0); _game.SnapCamera();
            s.RaiseEvent("shop_loyalty_card"); if (s.PendingEvent == null || string.IsNullOrEmpty(s.PendingEvent.Title)) throw new InvalidOperationException("Member event unavailable"); Shot("96-membership-offer"); s.ResolveEvent(0);
            s.RaiseEvent("money_found_coupon"); if (s.PendingEvent == null) throw new InvalidOperationException("Coupon event unavailable"); s.ResolveEvent(0); _game.Ui.CloseAllWindows();
            if (!s.HasEventMembership(s.MainShop) || s.EventCoupon == null) throw new InvalidOperationException("Actual membership or coupon state missing");
            _game.Ui.OpenShopEventTasks(); Shot("91-100-business-tasks"); _game.Ui.CloseAllWindows();
            s.RaiseEvent("shop_hot_item_rumor"); if (s.PendingEvent == null) throw new InvalidOperationException("Rumor procurement unavailable"); Shot("95-real-procurement-offer"); s.ResolveEvent(0);
            if (s.Online.Pending.Count != 1) throw new InvalidOperationException("Actual procurement order missing"); _game.Ui.CloseAllWindows();
            s.RaiseEvent("money_phone_scam"); if (s.PendingEvent == null) throw new InvalidOperationException("Scam unavailable"); s.ResolveEvent(1); _game.Ui.CloseAllWindows();
            _game.Ui.OpenShopEventTasks(); Shot("99-bank-verification-task");
            File.WriteAllText(Path.Combine(_outDir, "runtime-results.txt"), "Production scene/UI: membership purchase, saved coupon, business tasks, actual paid procurement and bank verification task. Canonical event JSON written by production EventScriptWriter.\n");
            Log("Finance 91-100 live UI, membership, coupon and paid procurement passed.");
        });
    }
}
