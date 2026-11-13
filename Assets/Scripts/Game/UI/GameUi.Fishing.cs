using System;
using PCShopSell.Core.Session;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private Window _fishingWindow;
        private FishingWaterGraphic _fishingWater;
        private FishingFightGraphic _fishingTrack;
        private Image _fishingPower, _fishingProgress, _fishingTension;
        private Text _fishingStatus, _fishingReadout, _fishingActionText;
        private Button _fishingAction;
        private bool _fishingPointerHeld, _fishingWasHeld;
        private FishingPhase _fishingRenderedPhase;
        public bool FishingInputActive => _fishingWindow != null && !_fishingWindow.Closed && S.FishingActive;
        public void OpenFishing()
        {
            if (_fishingWindow != null && !_fishingWindow.Closed) return;
            CloseAllWindows(); S.SetFishingInterfaceOpen(true);
            _fishingPointerHeld = _fishingWasHeld = false;
            _fishingWindow = OpenWindow("青湾垂钓", 1920, 1080, BuildFishingWindow, holds: false, onClosed: () =>
            { _fishingPointerHeld = _fishingWasHeld = false; S.SetFishingInterfaceOpen(false); _fishingWindow = null; },
                scrollContent: false, showTitleBar: false);
        }
        private void BuildFishingWindow(Window w)
        {
            var fish = S.CurrentFishing;
            _fishingRenderedPhase = fish?.Phase ?? FishingPhase.Ended;
            w.TimeStatusOverride = fish?.Phase == FishingPhase.Waiting ? "等待时世界继续" : "实时操作中";
            var panel = w.Root.Find("Panel") as RectTransform;
            if (w.Fitter != null) w.Fitter.enabled = false;
            if (panel != null) { panel.localScale = Vector3.one; panel.Fill(); }
            w.Content.GetComponent<VerticalLayoutGroup>().enabled = false;
            w.Content.Fill();
            _fishingWater = null; _fishingTrack = null; _fishingActionText = null; _fishingAction = null;
            _fishingPower = _fishingProgress = _fishingTension = null;
            var water = Ui.Rect(w.Content, "FishingPondScene"); water.Fill();
            _fishingWater = water.gameObject.AddComponent<FishingWaterGraphic>(); _fishingWater.raycastTarget = true;
            water.gameObject.AddComponent<FishingHoldInput>().Held = value => _fishingPointerHeld = value;
            _fishingWater.State = fish;
            var header = Ui.Panel(w.Content, "FishingSceneHeader", new Color(.07f, .13f, .14f, .92f));
            header.rectTransform.Anchor(0, 1, 1, 1, 0, -88, 0, 0);
            Ui.Label(header.transform, "青湾垂钓场", 32, color: Ui.Accent).rectTransform.Anchor(0, 0, .33f, 1, 34, 12, 0, 12);
            Ui.Label(header.transform, $"{S.Clock.Today} · 熟练度 {S.FishingLevel} · 本场钓获 {fish?.Catches ?? 0} 条", 18, TextAnchor.MiddleCenter)
                .rectTransform.Anchor(.32f, 0, .76f, 1, 0, 12, 0, 12);
            Ui.Button(header.transform, "结束钓鱼", () => { if (S.FishingActive) S.CancelCityActivity(fish.ActivityId, true); w.Close(); }, 19)
                .GetComponent<RectTransform>().Anchor(.83f, 0, 1, 1, 12, 20, 30, 20);
            _fishingStatus = Ui.Label(w.Content, S.FishingHint, 22, color: Ui.TextColor);
            _fishingStatus.rectTransform.Anchor(.08f, .80f, .83f, .87f);
            var rail = Ui.Panel(w.Content, "FishingControls", new Color(.06f, .13f, .15f, .97f));
            rail.rectTransform.Anchor(0, 0, 1, 0, 0, 0, 0, -164);
            _fishingReadout = Ui.Label(rail.transform, "", 20, color: Ui.Accent);
            _fishingReadout.rectTransform.Anchor(.025f, .72f, .70f, .99f);
            if (!S.FishingActive)
            {
                _fishingReadout.text = fish?.Message ?? "本场垂钓已结束，请返回场所菜单购买服务。";
                _fishingStatus.text = "收竿休息，下次再来。"; return;
            }
            if (fish.Phase == FishingPhase.Fighting)
            {
                var track = Ui.Rect(w.Content, "FishControlTrack"); track.Anchor(.895f, .36f, .947f, .73f);
                _fishingTrack = track.gameObject.AddComponent<FishingFightGraphic>(); _fishingTrack.raycastTarget = false;
                Ui.Label(w.Content, "让绿区跟住鱼", 18, TextAnchor.MiddleCenter).rectTransform.Anchor(.85f, .74f, .99f, .78f);
            }
            if (fish.Phase == FishingPhase.Ready)
            {
                var baits = Ui.Row(rail.transform, 40, 8); baits.Anchor(.025f, .31f, .55f, .65f);
                for (int i = 0; i < 3; i++)
                {
                    int index = i;
                    var dock = Ui.Button(w.Content, (fish.Spot == i ? "● " : "") + FishingCatalog.Spots[i], () => { S.SelectFishingSpot(index); w.Rebuild(); }, 17,
                        fish.Spot == i ? Ui.ButtonHot : Ui.ButtonBg);
                    dock.GetComponent<RectTransform>().Anchor(.25f + i * .17f, .245f, .33f + i * .17f, .29f);
                    var dockHit = Ui.Rect(w.Content, "DockHitArea" + i);
                    dockHit.Anchor(.26f + i * .17f, .29f, .32f + i * .17f, .435f);
                    var hitImage = dockHit.gameObject.AddComponent<Image>(); hitImage.color = Color.clear;
                    var hitButton = dockHit.gameObject.AddComponent<Button>(); hitButton.targetGraphic = hitImage;
                    hitButton.onClick.AddListener(() => { if (S.SelectFishingSpot(index)) w.Rebuild(); });
                    Ui.Size(Ui.Button(baits, FishingCatalog.Baits[i] + " ×" + fish.Baits[i], () => { S.SelectFishingBait(index); w.Rebuild(); }, 17,
                        fish.Bait == i ? Ui.ButtonHot : Ui.ButtonBg), -1, 36, 1);
                }
            }
            if (fish.Phase == FishingPhase.Ready || fish.Phase == FishingPhase.Charging)
            {
                var meter = Ui.Rect(rail.transform, "CastMeter"); meter.Anchor(.025f, .03f, .69f, .23f);
                _fishingPower = FishingMeter(meter, "甩竿力度", Ui.Accent);
            }
            if (fish.Phase == FishingPhase.Fighting)
            {
                var progress = Ui.Rect(rail.transform, "CatchMeter"); progress.Anchor(.025f, .35f, .35f, .62f);
                var tension = Ui.Rect(rail.transform, "TensionMeter"); tension.Anchor(.39f, .35f, .69f, .62f);
                _fishingProgress = FishingMeter(progress, "收鱼进度", Ui.Good);
                _fishingTension = FishingMeter(tension, "鱼线张力", Ui.Warn);
            }
            if (fish.Phase != FishingPhase.Landed)
            {
                var action = Ui.Button(rail.transform, "", null, 20, Ui.ButtonHot);
                _fishingAction = action;
                action.GetComponent<RectTransform>().Anchor(.72f, .25f, .985f, .85f);
                _fishingActionText = action.GetComponentInChildren<Text>();
                var input = action.gameObject.AddComponent<FishingHoldInput>(); input.Held = value => _fishingPointerHeld = value;
            }
            else
            {
                var disposition = Ui.Row(rail.transform, 44, 12); disposition.Anchor(.025f, .30f, .975f, .65f);
                Ui.Size(Ui.Button(disposition, "带走 · 放入背包食材", () => { S.KeepCaughtFish(); w.Rebuild(); }, 19, Ui.ButtonHot), -1, 44, 1);
                Ui.Size(Ui.Button(disposition, "放生 · 心情 +1", () => { S.ReleaseCaughtFish(); w.Rebuild(); }, 18), -1, 44, 1);
                foreach (string person in S.FishingCompanions)
                { string id = person; Ui.Size(Ui.Button(disposition, "送给 " + S.CityPerson(id)?.Name, () => { S.GiveCaughtFish(id); w.Rebuild(); }, 18), -1, 44, 1); }
            }
            if (fish.Phase == FishingPhase.Ready)
            {
                int selected = fish.Bait;
                Ui.Button(rail.transform, "补饵 +5 · ¥" + new[] { 100, 200, 500 }[selected], () => { S.BuyFishingBait(selected); w.Rebuild(); }, 16)
                    .GetComponent<RectTransform>().Anchor(.57f, .31f, .69f, .65f);
            }
            RefreshFishingGraphics();
        }
        private static Image FishingMeter(Transform parent, string name, Color color)
        {
            var row = Ui.Row(parent, 22, 10); Ui.Size(Ui.Label(row, name, 16), 100, 22);
            row.Fill();
            var back = Ui.Panel(row, "MeterBackground", Ui.ButtonBg); Ui.Size(back, -1, 22, 1);
            var fill = Ui.Panel(back.transform, "MeterFill", color); fill.raycastTarget = false;
            fill.rectTransform.Anchor(0, 0, 0, 1); return fill;
        }
        private void UpdateFishingInteraction(float dt)
        {
            if (_fishingWindow == null || _fishingWindow.Closed) return;
            if (!S.FishingActive)
            {
                if (_fishingRenderedPhase != FishingPhase.Ended) _fishingWindow.Rebuild();
                return;
            }
            bool held = _fishingPointerHeld || Input.GetKey(KeyCode.Space);
            if (held && !_fishingWasHeld)
            {
                if (S.CurrentFishing.Phase == FishingPhase.Ready) S.BeginFishingCast();
                else if (S.CurrentFishing.Phase == FishingPhase.Bite) S.HookFishingFish();
            }
            if (!held && _fishingWasHeld && S.CurrentFishing.Phase == FishingPhase.Charging) S.ReleaseFishingCast();
            _fishingWasHeld = held; S.AdvanceFishingInteraction(dt, held);
            // Keep the pressed pointer target alive throughout charging.
            var phase = S.CurrentFishing.Phase;
            if (phase != _fishingRenderedPhase && !(phase == FishingPhase.Charging && _fishingRenderedPhase == FishingPhase.Ready)) _fishingWindow.Rebuild();
            RefreshFishingGraphics();
        }
        private void RefreshFishingGraphics()
        {
            var f = S.CurrentFishing; if (f == null || _fishingStatus == null) return;
            _fishingStatus.text = f.Phase == FishingPhase.Ready ? "点钓台换位置，按住水面甩竿"
                : f.Phase == FishingPhase.Charging ? "松手甩出" : f.Phase == FishingPhase.Casting ? ""
                : f.Phase == FishingPhase.Waiting ? "等浮漂下沉…" : f.Phase == FishingPhase.Bite ? "咬钩了，点一下！"
                : f.Phase == FishingPhase.Fighting ? (f.LineTension >= .65 ? "鱼线绷紧了，松手！" : "按住收线，松开放线")
                : "上鱼了！";
            if (_fishingWater != null) { _fishingWater.State = f; _fishingWater.SetVerticesDirty(); }
            if (_fishingTrack != null) { _fishingTrack.State = f; _fishingTrack.ZoneSize = (float)S.FishingControlSize; _fishingTrack.SetVerticesDirty(); }
            void Meter(Image img, double amount) { if (img != null) img.rectTransform.Anchor(0, 0, (float)amount, 1); }
            Meter(_fishingPower, f.CastPower); Meter(_fishingProgress, f.CatchProgress); Meter(_fishingTension, f.LineTension);
            if (_fishingPower != null) _fishingPower.transform.parent.parent.gameObject.SetActive(f.Phase == FishingPhase.Charging);
            if (_fishingTension != null) _fishingTension.color = f.LineTension >= .65 ? new Color(.95f, .30f, .15f) : Ui.Accent;
            if (_fishingReadout != null) _fishingReadout.text = f.Phase == FishingPhase.Charging ? $"力度 {f.CastPower:P0} · 预计落点 {4 + 26 * f.CastPower:0.0} 米"
                : f.Phase == FishingPhase.Fighting ? (f.LineTension >= .65 ? "松开放线，张力就会下降" : FishingCatalog.Get(f.PendingFishId)?.BehaviorHint)
                : f.Phase == FishingPhase.Waiting ? $"落点 {f.CastDistance:0.0} 米 · {FishingCatalog.Baits[f.Bait]} · 等待浮漂下沉…"
                : f.Phase == FishingPhase.Bite ? $"咬钩！剩余 {Math.Max(0, 3 - f.PhaseSeconds):0.0} 秒"
                : f.Phase == FishingPhase.Ready ? (S.FishingCastBlocker ?? (f.Message?.StartsWith("本次包含") == true
                    ? "选好鱼饵就可以甩竿" : f.Message)) : f.Message;
            if (_fishingAction != null) _fishingAction.interactable = f.Phase != FishingPhase.Waiting && f.Phase != FishingPhase.Casting;
            if (_fishingActionText != null) _fishingActionText.text = f.Phase == FishingPhase.Ready ? "按住甩竿"
                : f.Phase == FishingPhase.Charging ? "松手甩出" : f.Phase == FishingPhase.Bite ? "提竿！"
                : f.Phase == FishingPhase.Fighting ? "按住收线 · 松开放线" : "等鱼咬钩…";
        }
        private void BuildFishBuyback(Window w)
        {
            Line(w.Content, "钓场回收 · 只出售背包里的真实鱼获", 19, Ui.Accent);
            Line(w.Content, "共 20 种 · 近岸、中水、远水各有鱼群 · 换饵会改变遇鱼机会", 16, Ui.Dim);
            foreach (var item in FishingCatalog.Fish)
            {
                var fish = item; int count = S.Backpack.Food.IngredientCount(fish.Id);
                Ui.Size(Ui.Button(w.Content, $"{fish.Name} · {fish.Kg:0.##} 千克/条 · 持有 {count} · 售出 1 条 ¥{fish.SalePrice}", () => { S.SellFishingCatch(fish.Id, 1); w.Rebuild(); }, 17, Ui.ButtonBg, count > 0), -1, 38);
                Line(w.Content, $"{fish.Rarity} · {new[] { "近岸", "中水", "远水" }[fish.Habitat]} · 偏好{FishingCatalog.Baits[fish.Bait]} · {fish.MovementName}", 15, Ui.Dim);
            }
        }
    }
    public sealed class FishingHoldInput : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public Action<bool> Held;
        public void OnPointerDown(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) Held?.Invoke(true); }
        public void OnPointerUp(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) Held?.Invoke(false); }
        private void OnDisable() => Held?.Invoke(false);
    }
}
