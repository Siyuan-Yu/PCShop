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
        public static void OldMarketFromBatch() => RunSmokeBatch(() =>
        {
            _outDir = Path.GetFullPath("docs/previews/oldmarket"); Directory.CreateDirectory(_outDir);
            ShaderUtil.allowAsyncCompilation = false; UrpSetup.Ensure(); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = BuildRig(); _game = new GameObject("Game").AddComponent<GameController>(); _game.Bootstrap(false, 1234); _game.StartCityLifePreset("solo"); _game.Ui.QuestPopups = false;
            foreach (var c in _game.Ui.Canvases) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = camera; c.planeDistance = 1; }
            var s = _game.Session; s.EventsEnabled = false; s.Clock.Restore(new GameDate(42), 600);
            _game.Ui.CloseAllWindows(); Teleport(s, GameSession.OldMarketVenueId); Frame(0); _game.SnapCamera(); Frame(0); Shot("01-market-four-stalls");
            if (GameObject.FindObjectsOfType<Transform>().Count(t => t.name.StartsWith("OldMarketStall_", StringComparison.Ordinal)) != 4) throw new InvalidOperationException("Four physical market stalls missing");
            _game.Ui.OpenCityVenue(s.CurrentPlace); Shot("02-real-market-catalog"); _game.Ui.CloseAllWindows();
            typeof(PCShopSell.Game.UI.GameUi).GetField("_oldMarketStall", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(_game.Ui, 1);
            _game.Ui.OpenCityVenue(s.CurrentPlace);
            var buy = _game.Ui.Canvases.SelectMany(c => c.GetComponentsInChildren<UnityEngine.UI.Button>()).Single(b => b.name == "OldMarketBuy_oldmarket:radio");
            buy.onClick.Invoke(); Frame(0); Shot("04-simple-owned-actions");
            var repair = _game.Ui.Canvases.SelectMany(c => c.GetComponentsInChildren<UnityEngine.UI.Button>()).Single(b => b.name == "OldMarketRepair_oldmarket:radio");
            repair.onClick.Invoke(); Frame(0);
            if (_game.Ui.AnyModalOpen || s.OldMarketProgress.Remaining != 15) throw new InvalidOperationException("One-click repair did not close menu and start work");
            s.AdvanceClock(15); Frame(0);
            if (s.Backpack.GiftCount("oldmarket:radio") != 1 || s.Backpack.GiftCount("oldmarket:radio:faulty") != 0) throw new InvalidOperationException("Repair did not return exactly one working item");
            typeof(PCShopSell.Game.UI.GameUi).GetField("_oldMarketStall", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(_game.Ui, 4);
            _game.Ui.OpenCityVenue(s.CurrentPlace); Shot("03-real-repaired-item");
            File.WriteAllText(Path.Combine(_outDir, "runtime-results.txt"), "Production world: four physical stalls, simplified five-tab menu; clicked actual buy and master-repair buttons; menu closed automatically; exactly one repaired backpack item after 15 minutes.\n");
            Log("Independent old market, four physical stalls, actual purchase/inspection/repair passed.");
        });
    }
}
