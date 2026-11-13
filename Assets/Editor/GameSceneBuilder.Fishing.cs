using System;
using System.IO;
using System.Linq;
using System.Reflection;
using PCShopSell.Core.Session;
using PCShopSell.Game;
using PCShopSell.Game.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using PCShopSell.Game.UI;

namespace PCShopSell.EditorTools
{
    public static partial class GameSceneBuilder
    {
        public static void FishingFromBatch() => RunSmokeBatch(SmokeFishing);
        private static void SmokeFishing()
        {
            _outDir = Path.GetFullPath("docs/previews/fishing"); Directory.CreateDirectory(_outDir);
            ShaderUtil.allowAsyncCompilation = false; UrpSetup.Ensure();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = BuildRig(); _game = new GameObject("Game").AddComponent<GameController>();
            _game.Bootstrap(false, 1234); _game.StartCityLifePreset("solo"); _game.Ui.QuestPopups = false;
            foreach (var canvas in _game.Ui.Canvases) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1; }
            var session = _game.Session; session.EventsEnabled = false; session.Clock.Restore(session.Clock.Today, 600);
            session.Time.SetSpeed(2); Teleport(session, GameSession.FishingVenueId); _game.Ui.CloseAllWindows();
            session.Time.SetPaused(true); Frame(0); _game.SnapCamera();
            var rig = (CameraRig)typeof(GameController).GetField("_rig", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_game);
            var venue = _game.World.Places[GameSession.FishingVenueId];
            rig.Snap(venue.Root.transform.TransformPoint(new Vector3(0, .5f, (float)venue.Place.Depth * .42f)), 13);
            Shot("01-fishing-venue");
            long before = session.Ledger.Balance.Cents;
            if (!session.SelectVenueService("fishing", 0)) throw new InvalidOperationException(session.VenueServiceBlocker("fishing", 0));
            session.AdvanceClock(2); Frame(0); _game.SnapCamera(); _game.Ui.OpenFishing();
            // Select the actual dock hit areas rather than a separate location menu.
            ClickFishingDock(1); session.AdvanceClock(2); Frame(0);
            if (session.CurrentFishing.Spot != 1 || session.CurrentFishing.Casts != 0) throw new InvalidOperationException("Dock selection cast a line or failed to move");
            ClickFishingDock(0); session.AdvanceClock(2); Frame(0);
            Shot("02-choose-spot-and-bait");
            session.Time.SetPaused(false);
            FishingPointer(true); for (int i = 0; i < 12; i++) Frame(.05f);
            Shot("02b-casting-power"); FishingPointer(false); Frame(.05f);
            Shot("02c-rod-flight"); for (int i = 0; i < 18; i++) Frame(.05f);
            if (session.CurrentFishing.Phase != FishingPhase.Waiting) throw new InvalidOperationException("The real cast input did not start waiting");
            Shot("03-waiting-for-a-bite");
            WaitForFishingBite(); Shot("04-reading-the-float");
            FightFishing();
            for (int attempt = 0; session.CurrentFishing.Phase != FishingPhase.Landed && attempt < 4; attempt++)
            {
                FishingPointer(true); for (int i = 0; i < 12; i++) Frame(.05f);
                FishingPointer(false); for (int i = 0; i < 18; i++) Frame(.05f);
                WaitForFishingBite(); FightFishing();
            }
            if (session.CurrentFishing.Phase != FishingPhase.Landed) throw new InvalidOperationException("No real catch after five casts");
            _game.Ui.OpenFishing(); Shot("05-caught-fish-options");
            string caught = session.CurrentFishing.PendingFishId; int stock = session.Backpack.Food.IngredientCount(caught);
            ClickFishing("带走 · 放入背包食材");
            if (session.Backpack.Food.IngredientCount(caught) != stock + 1) throw new InvalidOperationException("Catch did not enter the actual backpack");
            Shot("06-fish-kept-in-backpack");
            File.WriteAllText(Path.Combine(_outDir, "runtime-results.txt"),
                $"Production GameController.Step + uGUI pointer down/up; charge, casting flight, timed hook and real tracking fight; no forced outcome.\n"
                + $"Entry fee cents={before - session.Ledger.Balance.Cents}; casts={session.CurrentFishing.Casts}; catches={session.CurrentFishing.Catches}; kept={session.CurrentFishing.Kept}; fish={caught}; inventory={session.Backpack.Food.IngredientCount(caught)}\n");
            Log("Fishing graphics smoke finished with actual rod interaction, waiting, bite, catch and backpack delivery.");
        }
        private static void ClickFishing(string label)
        {
            Canvas.ForceUpdateCanvases();
            var button = _game.Ui.Canvases.SelectMany(c => c.GetComponentsInChildren<Button>())
                .FirstOrDefault(b => b.gameObject.activeInHierarchy && b.GetComponentInChildren<Text>()?.text == label);
            if (button == null || !button.interactable) throw new InvalidOperationException("Fishing UI button unavailable: " + label);
            button.onClick.Invoke(); Frame(0);
        }
        private static void WaitForFishingBite()
        {
            for (int i = 0; i < 1500 && _game.Session.CurrentFishing.Phase == FishingPhase.Waiting; i++) Frame(.05f);
            if (_game.Session.CurrentFishing.Phase != FishingPhase.Bite) throw new InvalidOperationException("The float never reached an actual bite");
        }
        private static void FishingPointer(bool held)
        {
            var input = _game.Ui.Canvases.SelectMany(c => c.GetComponentsInChildren<FishingHoldInput>()).FirstOrDefault();
            if (input == null) throw new InvalidOperationException("Fishing hold input missing");
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            if (held) input.OnPointerDown(pointer); else input.OnPointerUp(pointer);
        }
        private static void ClickFishingDock(int index)
        {
            var button = _game.Ui.Canvases.SelectMany(c => c.GetComponentsInChildren<Button>()).First(b => b.name == "DockHitArea" + index);
            button.onClick.Invoke(); Frame(0);
        }
        private static void FightFishing()
        {
            FishingPointer(true); Frame(.02f); FishingPointer(false);
            bool shot = false;
            for (int i = 0; i < 3000 && _game.Session.CurrentFishing.Phase == FishingPhase.Fighting; i++)
            {
                var fish = _game.Session.CurrentFishing;
                FishingPointer(fish.LineTension < .75 && fish.ControlPosition + fish.ControlVelocity * .18 < fish.FishPosition);
                Frame(.02f);
                if (!shot && fish.FightSeconds > 2) { Shot("04b-fighting-the-fish"); shot = true; }
            }
            if (_game.Session.CurrentFishing.Phase != FishingPhase.Landed) FishingPointer(false);
        }
    }
}
