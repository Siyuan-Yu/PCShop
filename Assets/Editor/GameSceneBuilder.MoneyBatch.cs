using System;
using System.IO;
using System.Linq;
using System.Reflection;
using PCShopSell.Core.Session;
using PCShopSell.Core.Common;
using PCShopSell.Core.Time;
using PCShopSell.Game;
using PCShopSell.Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PCShopSell.EditorTools
{
    public static partial class GameSceneBuilder
    {
        public static void MoneyBatchFromBatch() => RunSmokeBatch(() =>
        {
            string events = "Assets/Resources/Data/events.json";
            File.WriteAllText(events, EventScriptWriter.Write(EventScriptReader.Read(File.ReadAllText(events))));
            _outDir = Path.GetFullPath("docs/previews/money101-110"); Directory.CreateDirectory(_outDir);
            ShaderUtil.allowAsyncCompilation = false; UrpSetup.Ensure(); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = BuildRig(); _game = new GameObject("Game").AddComponent<GameController>(); _game.Bootstrap(false, 1234); _game.StartCityLifePreset("solo"); _game.Ui.QuestPopups = false;
            foreach (var c in _game.Ui.Canvases) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = camera; c.planeDistance = 1; }
            var s = _game.Session; s.EventsEnabled = false; s.Player.Health = s.Player.Hunger = s.Player.Energy = 100;
            _game.Ui.CloseAllWindows(); Teleport(s, s.MainShop.Place.Id); Frame(0); _game.SnapCamera();
            if (!s.BuyEventScratchTicket()) throw new InvalidOperationException("Actual ticket purchase failed");
            _game.Ui.OpenEventScratch(); Shot("103-ticket-covered");
            var scratch = UnityEngine.Object.FindObjectOfType<ScratchTicketGraphic>(); if (scratch == null) throw new InvalidOperationException("Scratch surface missing");
            Canvas.ForceUpdateCanvases(); var rect = scratch.rectTransform; var r = rect.rect;
            var module = scratch.GetComponentInParent<Canvas>().GetComponent<GraphicRaycaster>();
            foreach (int y in new[] { 1, 4, 7 }) foreach (int x in new[] { 1, 4, 7, 10, 13, 16 })
            {
                var pos = rect.TransformPoint(new Vector3(r.xMin + (x + .5f) * r.width / 18, r.yMin + (y + .5f) * r.height / 8));
                var e = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(camera, pos),
                    button = PointerEventData.InputButton.Left, pointerPressRaycast = new RaycastResult { module = module } };
                scratch.OnDrag(e);
                if (y == 4 && x == 7) Shot("103-ticket-scratching");
            }
            if (!s.CanClaimScratch) throw new InvalidOperationException("Actual pointer dragging did not reveal ticket");
            var claim = _game.Ui.Canvases.SelectMany(c => c.GetComponentsInChildren<Button>()).First(b => b.GetComponentInChildren<Text>()?.text == "兑奖");
            claim.onClick.Invoke(); if (!s.ScratchTicket.Paid) throw new InvalidOperationException("UI prize settlement failed"); Shot("103-ticket-settled"); _game.Ui.CloseAllWindows();
            s.RaiseEvent("money_insurance_agent"); if (s.PendingEvent == null) throw new InvalidOperationException("Insurance event missing"); s.ResolveEvent(0); _game.Ui.CloseAllWindows();
            if (!s.HasHospitalCoverage) throw new InvalidOperationException("Annual policy missing");
            s.Player.Intelligence = 70; s.RaiseEvent("money_invest_tip"); if (s.PendingEvent == null) throw new InvalidOperationException("Investment event missing"); Shot("107-investment-offer"); s.ResolveEvent(1); _game.Ui.CloseAllWindows();
            _game.Ui.OpenShopEventTasks(); Shot("106-107-policy-and-investment"); _game.Ui.CloseAllWindows();
            typeof(GameSession).GetMethod("Hospitalize", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(s, null);
            if (!s.Ledger.Entries.Any(e => e.Category == "住院保险理赔" && e.Amount == Money.FromYuan(160))) throw new InvalidOperationException("Actual hospitalization did not reimburse 80 percent");
            _game.Ui.CloseAllWindows(); s.Backpack.AddFurniture("fan_new", 2); Teleport(s, GameSession.OldMarketVenueId); s.Clock.Restore(s.Clock.Today, 660); Frame(0); _game.SnapCamera();
            _game.Ui.OpenEventIdleSale(); Shot("109-real-idle-sale");
            var sell = _game.Ui.Canvases.SelectMany(c => c.GetComponentsInChildren<Button>()).First(b => b.GetComponentInChildren<Text>()?.text == "卖1件");
            var cash = s.Ledger.Balance; var price = s.EventIdleItems.Single(i => i.Id == "fan_new").Price; sell.onClick.Invoke();
            if (s.Backpack.FurnitureCount("fan_new") != 1 || s.Ledger.Balance != cash + price) throw new InvalidOperationException("Actual resale inventory/cash mismatch");
            File.WriteAllText(Path.Combine(_outDir, "runtime-results.txt"), "Production UI: actual ticket purchase, pointer-event scratching, UI prize settlement; annual policy; paid private investment; actual hospitalization 80% reimbursement; actual backpack furniture sale at old market. Owned furniture is an editor fixture.\n");
            Log("Money 101-110 actual scratching, annual policy, hospitalization, investment and resale passed.");
        });
    }
}
