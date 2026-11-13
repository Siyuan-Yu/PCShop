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

namespace PCShopSell.EditorTools
{
    public static partial class GameSceneBuilder
    {
        public static void ExhibitionBatchFromBatch() => RunSmokeBatch(() =>
        {
            _outDir = Path.GetFullPath("docs/previews/exhibition71-80"); Directory.CreateDirectory(_outDir);
            ShaderUtil.allowAsyncCompilation = false; UrpSetup.Ensure(); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = BuildRig(); _game = new GameObject("Game").AddComponent<GameController>(); _game.Bootstrap(false, 1234); _game.StartCityLifePreset("solo"); _game.Ui.QuestPopups = false;
            foreach (var c in _game.Ui.Canvases) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = camera; c.planeDistance = 1; }
            var s = _game.Session; s.EventsEnabled = false;
            ((DistrictProgress)typeof(GameSession).GetField("_district", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(s)).UnlockedTier = 3;
            s.Player.Health = 100; s.Player.Hunger = 100; s.Player.Energy = 100; s.Clock.Restore(new GameDate(42), 600); _game.Ui.CloseAllWindows();
            s.RaiseEvent("act_expo_hanzheng"); if (s.PendingEvent == null) throw new InvalidOperationException("Exhibition invitation missing");
            s.ResolveEvent(1); var a = s.CityActivities.Last(x => x.Active);
            s.Clock.Restore(new GameDate((int)(a.ScheduledStart / 1440)), (int)(a.ScheduledStart % 1440)); Teleport(s, a.VenueId); s.PrepareVenueCompanions(s.CurrentPlace); s.AdvanceClock(3); Frame(0);
            if (a.State != CityActivityPhase.Running) throw new InvalidOperationException(a.EndReason);
            _game.SnapCamera(); s.AdvanceClock(4); Frame(0); Shot("74-01-real-exhibit-tasks");
            typeof(PCShopSell.Game.UI.GameUi).GetMethod("OpenCityActivityDetail", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(_game.Ui, new object[] { a.Id }); Shot("74-02-onsite-work-options"); _game.Ui.CloseAllWindows();
            var cash = s.Ledger.Balance;
            for (int i = 1; i <= 3; i++)
            {
                if (!s.ChooseCityActivity(a.Id, i + 1)) throw new InvalidOperationException("Could not choose exhibition fix");
                s.AdvanceClock(5); Frame(0);
                if (i < 3) { s.ChooseCityActivity(a.Id, 1); s.AdvanceClock(4); Frame(0); }
            }
            if (s.OutdoorActivityState(a.Id).Correct != 3) throw new InvalidOperationException("Exhibition fixes did not finish");
            Shot("74-03-fixed-exhibits"); s.AdvanceClock(100); Frame(0);
            if (s.Ledger.Balance.Cents - cash.Cents != 150000) throw new InvalidOperationException("Actual 1500 labour payment missing");
            File.WriteAllText(Path.Combine(_outDir, "runtime-results.txt"), "Production controller, monthly exhibit props and actual on-site task choices; three five-minute fixes; 1500 yuan labour paid only after completion.\n");
            Log("Exhibition physical props, timed tasks and actual labour payment passed.");
        });
    }
}
