using System;
using System.IO;
using System.Linq;
using PCShopSell.Core.Common;
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
            UnityEngine.UI.Button Button(string name) => _game.Ui.Canvases
                .SelectMany(c => c.GetComponentsInChildren<UnityEngine.UI.Button>()).Single(b => b.name == name);
            _game.Ui.OpenCityVenue(s.CurrentPlace);
            Shot("02-market-reception");
            var cash = s.Ledger.Balance;
            Button("BuyMarketHuntPass").onClick.Invoke(); Frame(0); Shot("03-market-hunt");
            if (s.MarketHuntRemaining != 1 || s.Ledger.Balance != cash - Money.FromYuan(OldMarketHuntCatalog.Prices[0])
                || !Button("SearchProp_radio").IsInteractable())
                throw new InvalidOperationException("Counter purchase did not open the existing hunt with one paid attempt");
            if (!s.BeginMarketHunt("radio") || !s.ClaimMarketFind() || s.MarketHuntRemaining != 0)
                throw new InvalidOperationException("Paid hunt did not return an item");
            _game.Ui.CloseAllWindows();
            s.Backpack.AddGift("oldmarket:radio:faulty", 1);
            _game.Ui.OpenCityVenue(s.CurrentPlace);
            Button("OldMarketServices").onClick.Invoke(); Frame(0); Shot("04-legacy-repair");
            Button("OldMarketRepair_oldmarket:radio").onClick.Invoke(); Frame(0);
            if (_game.Ui.AnyModalOpen || s.OldMarketProgress.Remaining != 15) throw new InvalidOperationException("One-click repair did not close menu and start work");
            s.AdvanceClock(15); Frame(0);
            if (s.Backpack.GiftCount("oldmarket:radio") != 1 || s.Backpack.GiftCount("oldmarket:radio:faulty") != 0) throw new InvalidOperationException("Repair did not return exactly one working item");
            File.WriteAllText(Path.Combine(_outDir, "runtime-results.txt"), "Production world: four physical stalls; counter purchase opened the existing hunt; used one paid search; repaired one legacy item.\n");
            Log("旧物市场接待、买次数、淘货和旧存档物品维修通过。");
        });
    }
}
