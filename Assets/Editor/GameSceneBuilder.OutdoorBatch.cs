using System;
using System.IO;
using System.Linq;
using System.Reflection;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.EditorTools
{
    public static partial class GameSceneBuilder
    {
        public static void OutdoorBatchFromBatch() => RunSmokeBatch(() =>
        {
            _outDir = Path.GetFullPath("docs/previews/outdoor61-70"); Directory.CreateDirectory(_outDir);
            ShaderUtil.allowAsyncCompilation = false; UrpSetup.Ensure();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = BuildRig(); _game = new GameObject("Game").AddComponent<GameController>(); _game.Bootstrap(false, 1234);
            _game.StartCityLifePreset("solo"); _game.Ui.QuestPopups = false;
            foreach (var c in _game.Ui.Canvases) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = camera; c.planeDistance = 1; }
            var s = _game.Session; s.EventsEnabled = false; s.DebugRentStarterShop(computerScenario: true); s.DebugFitShop();
            s.MainShop.Sim.Settings.BaseCustomersPerHour = 0; s.Player.Health = 100; s.Player.Hunger = 100; s.Player.Energy = 100;
            s.Clock.Restore(new GameDate(42), 600); s.Meet("wang", 60); _game.Ui.CloseAllWindows();
            if (!s.HasOpenedComputerShop) throw new InvalidOperationException("The actual PC shop was not opened");
            s.RaiseEvent("act_market_wang"); if (s.PendingEvent == null) throw new InvalidOperationException("GPU invitation unavailable after opening a PC shop");
            s.ResolveEvent(0); var a = s.CityActivities.Last(x => x.Active);
            s.Clock.Restore(new GameDate((int)(a.ScheduledStart / 1440)), (int)(a.ScheduledStart % 1440)); Teleport(s, a.VenueId);
            s.PrepareVenueCompanions(s.CurrentPlace); s.AdvanceClock(3); Frame(0); _game.SnapCamera();
            if (a.State != CityActivityPhase.Running) throw new InvalidOperationException(a.EndReason);
            void Detail(bool bottom = false)
            {
                _game.Ui.CloseAllWindows(); typeof(PCShopSell.Game.UI.GameUi).GetMethod("OpenCityActivityDetail", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(_game.Ui, new object[] { a.Id });
                Canvas.ForceUpdateCanvases();
                if (bottom)
                {
                    var scroll = _game.Ui.Canvases.SelectMany(c => c.GetComponentsInChildren<ScrollRect>()).FirstOrDefault(r => r.GetComponentInChildren<Dropdown>() != null);
                    if (scroll != null) { scroll.StopMovement(); scroll.verticalNormalizedPosition = 0; }
                }
            }
            Detail(); Shot("70-01-actual-gpu-visit"); _game.Ui.CloseAllWindows(); s.AdvanceClock(4);
            if (!s.ChooseCityActivity(a.Id, 2)) throw new InvalidOperationException("Could not begin actual sample test");
            s.AdvanceClock(5); if (!s.OutdoorActivityState(a.Id).Tested) throw new InvalidOperationException("Sample test did not finish after five minutes");
            Detail(true); Shot("70-02-tested-model-and-order"); _game.Ui.CloseAllWindows();
            if (!s.OrderOutdoorMarketGoods(a.Id, 3)) throw new InvalidOperationException("Actual GPU purchase failed");
            if (!s.Couriers.Any(c => c.Items.Any(i => i.Id == "gpu1" && i.Count == 3))) throw new InvalidOperationException("No real three-GPU delivery");
            Detail(true); Shot("70-03-real-purchase-and-delivery");
            File.WriteAllText(Path.Combine(_outDir, "runtime-results.txt"), "Production GameController and activity UI; opened PC shop; actual attendance, five-minute sample test, purchase and three-GPU courier delivery.\n");
            Log("Outdoor batch 70 actual shop gate, timed sample test and physical purchase smoke passed.");
        });
    }
}
