using System;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.World;
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
        private RectTransform _fishingBiteHint;
        private Button _fishingAction;
        private bool _fishingPointerHeld, _fishingWasHeld;
        private FishingPhase _fishingRenderedPhase;
        public bool FishingInputActive => _fishingWindow != null && !_fishingWindow.Closed && S.FishingActive;

        private void OpenFishingVenue(Place place, string activityId = null)
        {
            bool here = S.CurrentPlace == place;
            if (here && S.FishingActive) { OpenFishing(); return; }
            if (here) S.PrepareVenueCompanions(place, activityId);

            var sponsor = new VenueSponsorView();
            OpenWindow(place.Name, 1060, 620, w =>
            {
                BuildVenueSponsorHeader(w, place, sponsor);
                if (BuildVenueSponsorPage(w, place, sponsor, w.Content)) return;
                var booking = here ? S.PendingVenueActivity(place, activityId) : null;
                if (!GameSession.IsInteractiveFishingActivity(booking)) booking = null;
                var goods = BuildRosterFrame(w, place, 460, scrollGoods: true, rosterWidth: 165);
                Line(goods, booking == null ? "购买抛竿次数" : S.CityActivityService(booking).Name, 24, Ui.Accent);
                if (booking != null)
                {
                    foreach (var companion in booking.Participants)
                        Line(goods, (S.CityPerson(companion.PersonId)?.Name ?? "同行者")
                            + " · " + CityParticipantText(companion.Phase), 17);
                }
                Para(goods, "含租杆和 12 份鱼饵。垂钓时游戏时钟暂停，收竿后按已用次数结算时间。", 17, Ui.Dim);
                var definitions = booking == null
                    ? FishingCatalog.Passes.Select(pass => CityLifeCatalog.Activity(pass.Id)).Where(pass => !pass.EventOnly).ToArray()
                    : new[] { S.CityActivityService(booking) };

                BuildVenuePasses(w, goods, definitions, booking, here, "钓鱼", "FishingPass_", OpenFishing);

                var footer = Ui.Row(w.Content, 46, 10);
                if (!here)
                {
                    Ui.Size(Ui.Button(footer, "前往钓场", () => { CloseAllWindows(); _ctrl.GoToPlace(place); },
                        18, Ui.ButtonHot), 0, 46, 1);
                    return;
                }

                int count = FishingCatalog.Fish.Sum(fish => S.Backpack.Food.IngredientCount(fish.Id));
                Ui.Size(Ui.Button(footer, $"鱼获回收 · 背包 {count} 条", () => OpenFishBuyback(w), 17), 0, 46, 1);
                Ui.Size(Ui.Button(footer, "返回街上", () => { w.Close(); _ctrl.LeaveCurrentPlace(); }, 17, Ui.ButtonSecondary), 180, 46);
            }, scrollContent: false);
        }

        public void OpenFishing()
        {
            if (!S.FishingActive || _fishingWindow != null && !_fishingWindow.Closed) return;
            CloseAllWindows();
            _fishingPointerHeld = _fishingWasHeld = false;
            _fishingWindow = OpenWindow("青湾垂钓", 1920, 1080, BuildFishingWindow, holds: false, closable: false, onClosed: () =>
            { _fishingPointerHeld = _fishingWasHeld = false; S.CloseFishingInterface(); _fishingWindow = null; },
                scrollContent: false, showTitleBar: false);
        }
        private void BuildFishingWindow(Window w)
        {
            var fish = S.CurrentFishing;
            _fishingRenderedPhase = fish?.Phase ?? FishingPhase.Ended;
            w.TimeStatusOverride = "时间暂停 · 收竿后结算";
            var panel = w.Root.Find("Panel") as RectTransform;
            if (w.Fitter != null) w.Fitter.enabled = false;
            if (panel != null) { panel.localScale = Vector3.one; panel.Fill(); }
            w.Content.GetComponent<VerticalLayoutGroup>().enabled = false;
            w.Content.Fill();
            _fishingWater = null; _fishingTrack = null; _fishingActionText = null; _fishingAction = null;
            _fishingPower = _fishingProgress = _fishingTension = null;
            _fishingStatus = _fishingReadout = null;
            _fishingBiteHint = null;
            bool illustrated = BuildLeisureBackdrop(w.Content, "event_fishing_pond_play", fill: false);
            var water = Ui.Rect(w.Content, "FishingPondScene"); water.Fill();
            _fishingWater = water.gameObject.AddComponent<FishingWaterGraphic>(); _fishingWater.raycastTarget = true;
            _fishingWater.Illustrated = illustrated;
            water.gameObject.AddComponent<FishingHoldInput>().Held = value => _fishingPointerHeld = value;
            _fishingWater.State = fish;
            var header = Ui.Panel(w.Content, "FishingSceneHeader", Ui.Chrome);
            header.rectTransform.Anchor(0, 1, 1, 1, 0, -88, 0, 0);
            Ui.Label(header.transform, "青湾垂钓场", 32, color: Ui.Accent).rectTransform.Anchor(0, 0, .33f, 1, 34, 12, 0, 12);
            var companions = S.FishingCompanions;
            if (companions.Length > 0)
            {
                var roster = Ui.Row(w.Content, 40, 8).Anchor(.02f, 1, .42f, 1, 0, -142, 0, 96);
                foreach (string person in companions)
                {
                    PortraitThumb(roster, person, 34, null);
                    Ui.Size(Ui.Label(roster, S.CityPerson(person)?.Name ?? "同行者", 16, color: Ui.Accent), 78, 38);
                }
            }
            if (fish?.Phase == FishingPhase.Ready && S.RunningCityActivity is CityActivity outing)
            {
                var service = S.CityActivityService(outing);
                var topics = ActivityTopicIndices(outing, service);
                var talks = Ui.Rect(w.Content, "FishingConversation").Anchor(.025f, .64f, .27f, .82f);
                Ui.VList(talks, 6, 0);
                BuildRelationshipOutingButton(talks, outing, w.RequestRebuild, "继续钓鱼");
                if (topics.Length > 0)
                {
                    foreach (int topic in topics)
                    {
                        bool unused = !outing.Choices.Contains(topic.ToString()) && (outing.DefinitionId != "social_60" || fish.Style == 0);
                        Ui.Size(Ui.Button(talks, service.Choices[topic], () =>
                        {
                            if (SelectCityActivityOption(outing, service, topic, "继续钓鱼")) w.RequestRebuild();
                        }, 17, Ui.ButtonBg, unused), -1, 40);
                    }
                }
            }
            Ui.Label(header.transform, $"剩余 {S.ManualAttemptsLeft} 次 · 本场钓获 {fish?.Catches ?? 0} 条 · 时间暂停", 18, TextAnchor.MiddleCenter)
                .rectTransform.Anchor(.32f, 0, .76f, 1, 0, 12, 0, 12);
            void FinishFishing()
            {
                if (S.FishingActive && !S.CancelCityActivity(fish.ActivityId, true)) return;
                w.Close();
                if (S.IsGameOver || S.CurrentPlace?.Id != GameSession.FishingVenueId) return;
                OpenFishingVenue(S.CurrentPlace);
                if (fish != null) OpenCityActivitySettlement(fish.ActivityId);
            }
            Ui.Button(header.transform, S.FishingActive ? "收竿并结算" : "返回钓场", FinishFishing,
                19, Ui.ButtonBg, !S.FishingActive || fish?.Phase == FishingPhase.Ready)
                .GetComponent<RectTransform>().Anchor(.83f, 0, 1, 1, 12, 20, 30, 20);
            if (S.FishingActive && fish.Phase == FishingPhase.Landed)
            {
                BuildFishingCatchResult(w, fish);
                return;
            }
            if (S.FishingActive && fish.Phase is FishingPhase.Casting or FishingPhase.Waiting)
            {
                var wait = Ui.Panel(w.Content, "WatchFloat", Ui.Chrome);
                wait.raycastTarget = false;
                wait.rectTransform.Anchor(.36f, .055f, .64f, .112f);
                Ui.Label(wait.transform, fish.Phase == FishingPhase.Casting ? "浮漂即将落水" : "看着浮漂，鱼咬钩时提竿",
                    19, TextAnchor.MiddleCenter, Ui.Accent).rectTransform.Fill(8);
                return;
            }

            if (S.FishingActive && fish.Phase == FishingPhase.Bite)
            {
                var hint = Ui.Panel(w.Content, "BiteAtFloat", Ui.PanelBg);
                hint.raycastTarget = false;
                _fishingBiteHint = hint.rectTransform;
                Ui.Label(hint.transform, "咬钩了！", 22, TextAnchor.MiddleCenter, Ui.Accent).rectTransform.Fill(6);
                var bite = Ui.Panel(w.Content, "LiftRod", Ui.PanelBg).rectTransform.Anchor(.34f, .055f, .66f, .16f);
                _fishingReadout = Ui.Label(bite, "", 20, color: Ui.Accent);
                _fishingReadout.rectTransform.Anchor(.05f, .1f, .55f, .90f);
                _fishingAction = Ui.Button(bite, "提竿", null, 22, Ui.ButtonHot);
                _fishingAction.GetComponent<RectTransform>().Anchor(.58f, .19f, .96f, .81f);
                _fishingAction.gameObject.AddComponent<FishingHoldInput>().Held = value => _fishingPointerHeld = value;
                RefreshFishingGraphics();
                return;
            }

            _fishingStatus = Ui.Label(w.Content, S.FishingHint, 22, TextAnchor.MiddleCenter, Ui.Accent);
            var status = Ui.Panel(w.Content, "FishingHint", Ui.PanelBg).rectTransform.Anchor(.32f, .80f, .68f, .857f);
            status.GetComponent<Image>().raycastTarget = false;
            _fishingStatus.rectTransform.SetParent(status, false);
            _fishingStatus.rectTransform.Fill(7);
            var rail = Ui.Panel(w.Content, "FishingControls", Ui.Chrome);
            rail.rectTransform.Anchor(0, 0, 1, 0, 0, 0, 0, -164);
            _fishingReadout = Ui.Label(rail.transform, "", 20, color: Ui.Accent);
            _fishingReadout.rectTransform.Anchor(.025f, .72f, .70f, .99f);
            if (!S.FishingActive)
            {
                _fishingReadout.text = fish?.Message ?? "本场垂钓已结束。";
                _fishingStatus.text = "收竿休息，下次再来。"; return;
            }
            if (fish.Phase == FishingPhase.Fighting)
            {
                var control = Ui.Panel(w.Content, "FishControl", Ui.PanelBg).rectTransform.Anchor(.835f, .32f, .97f, .75f);
                control.GetComponent<Image>().raycastTarget = false;
                var track = Ui.Rect(control, "FishControlTrack").Anchor(.30f, .07f, .70f, .84f);
                _fishingTrack = track.gameObject.AddComponent<FishingFightGraphic>(); _fishingTrack.raycastTarget = false;
                Ui.Label(control, "让绿区跟住鱼", 18, TextAnchor.MiddleCenter).rectTransform.Anchor(0, .84f, 1, .98f);
            }
            if (fish.Phase == FishingPhase.Ready && S.ManualAttemptsLeft > 0)
            {
                var baits = Ui.Row(rail.transform, 40, 8); baits.Anchor(.025f, .31f, .55f, .65f);
                for (int i = 0; i < 3; i++)
                {
                    int index = i;
                    var dock = Ui.Button(w.Content, (fish.Spot == i ? "● " : "") + FishingCatalog.Spots[i], () => { S.SelectFishingSpot(index); w.Rebuild(); }, 17,
                        Ui.SelectedButton(fish.Spot == i));
                    float dockX = _fishingWater.DockCenter(i);
                    dock.GetComponent<RectTransform>().Anchor(dockX - .058f, .185f, dockX + .058f, .228f);
                    var dockHit = Ui.Rect(w.Content, "DockHitArea" + i);
                    dockHit.Anchor(dockX - .052f, .235f, dockX + .052f, .38f);
                    var hitImage = dockHit.gameObject.AddComponent<Image>(); hitImage.color = Color.clear;
                    var hitButton = dockHit.gameObject.AddComponent<Button>(); hitButton.targetGraphic = hitImage;
                    hitButton.onClick.AddListener(() => { if (S.SelectFishingSpot(index)) w.Rebuild(); });
                    var bait = Ui.Size(Ui.Button(baits, FishingCatalog.Baits[i] + " ×" + fish.Baits[i], () => { S.SelectFishingBait(index); w.Rebuild(); }, 17,
                        Ui.SelectedButton(fish.Bait == i)), -1, 36, 1);
                    ItemButtonPortrait(bait, i == 0 ? "fishing:dough" : i == 1 ? "fishing:worm" : "fishing:lure", 28);
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
            if (fish.Phase == FishingPhase.Ready && S.ManualAttemptsLeft == 0)
                Ui.Button(rail.transform, "收竿并结算", FinishFishing, 20, Ui.ButtonHot)
                    .GetComponent<RectTransform>().Anchor(.72f, .25f, .985f, .85f);
            else
            {
                var action = Ui.Button(rail.transform, "", null, 20, Ui.ButtonHot);
                _fishingAction = action;
                action.GetComponent<RectTransform>().Anchor(.72f, .25f, .985f, .85f);
                _fishingActionText = action.GetComponentInChildren<Text>();
                var input = action.gameObject.AddComponent<FishingHoldInput>(); input.Held = value => _fishingPointerHeld = value;
            }
            if (fish.Phase == FishingPhase.Ready && S.ManualAttemptsLeft > 0)
            {
                int selected = fish.Bait;
                Ui.Button(rail.transform, "补饵 +5 · ¥" + new[] { 100, 200, 500 }[selected], () => { S.BuyFishingBait(selected); w.Rebuild(); }, 16)
                    .GetComponent<RectTransform>().Anchor(.57f, .31f, .69f, .65f);
            }
            RefreshFishingGraphics();
        }

        private void BuildFishingCatchResult(Window w, FishingState state)
        {
            var fish = FishingCatalog.Get(state.PendingFishId);
            var shade = Ui.Panel(w.Content, "FishingCatchBackdrop", Ui.WorldShade);
            shade.rectTransform.Fill();
            var result = Ui.Panel(shade.transform, "FishingCatchResult", Ui.PanelBg2).rectTransform;
            result.Anchor(.32f, .5f, .68f, .5f);
            Ui.VList(result, 10, 22);
            result.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            const int portraitSize = 208;
            var summary = Ui.Row(result, fish == null ? 92 : portraitSize, 18);
            if (fish != null) ItemPortrait(summary, fish.Id, portraitSize);
            var details = Ui.Size(Ui.Rect(summary, "FishingCatchDetails"), 0, -1, 1);
            Ui.VList(details, 4, 0).childAlignment = TextAnchor.MiddleLeft;
            void Caption(string text, int font, int height, Color color)
                => Ui.Size(Ui.Label(details, text, font, TextAnchor.MiddleLeft, color), -1, height);
            Caption("本次鱼获", 16, 23, Ui.Dim);
            Caption(fish?.Name ?? "鱼已上岸", 24, 34, Ui.Accent);
            if (fish != null)
            {
                Caption($"{fish.Rarity} · {fish.Kg:0.##} 千克", 17, 25, Ui.TextColor);
                Caption($"回收价 {Money.FromYuan(fish.SalePrice)}", 17, 25, Ui.TextColor);
            }

            var actions = Ui.Row(result, 48, 12);
            bool canKeep = fish != null && fish.Kg <= S.FreeKg + 1e-9;
            Ui.Size(Ui.Button(actions, "带走 · 放入背包", () => { if (S.KeepCaughtFish()) w.Rebuild(); }, 17,
                Ui.ButtonHot, canKeep).Tip(canKeep ? "放进背包食材。" : "背包负重不足。"), 0, 48, 1);
            Ui.Size(Ui.Button(actions, "放生 · 心情 +1", () => { if (S.ReleaseCaughtFish()) w.Rebuild(); }, 17), 0, 48, 1);
            var companions = S.FishingCompanions;
            if (companions.Length == 0) return;
            var gifts = Ui.Row(result, 42, 8);
            foreach (string id in companions)
                Ui.Size(Ui.Button(gifts, "送给 " + S.CityPerson(id)?.Name, () => { if (S.GiveCaughtFish(id)) w.Rebuild(); }, 17), 0, 42, 1);
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
            if (_fishingWindow == null || _fishingWindow.Closed)
            {
                if (S.FishingActive && !AnyModalOpen && !MainMenuOpen) OpenFishing();
                return;
            }
            if (!S.FishingActive)
            {
                if (_fishingRenderedPhase != FishingPhase.Ended) _fishingWindow.Rebuild();
                return;
            }
            if (_windows.LastOrDefault() != _fishingWindow) return;
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
            var f = S.CurrentFishing;
            if (f == null) return;
            if (_fishingWater != null) { _fishingWater.State = f; _fishingWater.SetVerticesDirty(); }
            if (_fishingBiteHint != null && _fishingWater != null)
            {
                Vector2 p = _fishingWater.BobberPosition;
                _fishingBiteHint.Anchor(p.x - .063f, p.y + .039f, p.x + .063f, p.y + .090f);
                _fishingReadout.text = $"还剩 {Math.Max(0, FishingState.BiteWindowSeconds - f.PhaseSeconds):0.0} 秒";
                return;
            }
            if (_fishingStatus == null) return;
            _fishingStatus.text = f.Phase switch
            {
                FishingPhase.Ready => "选钓位，按住水面甩竿",
                FishingPhase.Charging => "松手甩出",
                FishingPhase.Fighting => f.LineTension >= .65 ? "鱼线绷紧了，松手！" : "按住收线，松开放线",
                FishingPhase.Ended => "收竿休息，下次再来。",
                _ => ""
            };
            if (_fishingTrack != null) { _fishingTrack.State = f; _fishingTrack.ZoneSize = (float)S.FishingControlSize; _fishingTrack.SetVerticesDirty(); }
            void Meter(Image img, double amount) { if (img != null) img.rectTransform.Anchor(0, 0, (float)amount, 1); }
            Meter(_fishingPower, f.CastPower); Meter(_fishingProgress, f.CatchProgress); Meter(_fishingTension, f.LineTension);
            if (_fishingPower != null) _fishingPower.transform.parent.parent.gameObject.SetActive(f.Phase == FishingPhase.Charging);
            if (_fishingTension != null) _fishingTension.color = f.LineTension >= .65 ? new Color(.95f, .30f, .15f) : Ui.Accent;
            if (_fishingReadout != null) _fishingReadout.text = f.Phase == FishingPhase.Charging ? $"力度 {f.CastPower:P0} · 预计落点 {4 + 26 * f.CastPower:0.0} 米"
                : f.Phase == FishingPhase.Fighting ? (f.LineTension >= .65 ? "松开放线，张力就会下降" : FishingCatalog.Get(f.PendingFishId)?.BehaviorHint)
                : f.Phase == FishingPhase.Ready ? (S.FishingCastBlocker ?? (f.Message?.StartsWith("本次包含") == true
                    ? "选好鱼饵就可以甩竿" : f.Message)) : f.Message;
            if (_fishingAction != null) _fishingAction.interactable = f.Phase == FishingPhase.Ready
                ? S.FishingCastBlocker == null : f.Phase != FishingPhase.Waiting && f.Phase != FishingPhase.Casting;
            if (_fishingActionText != null) _fishingActionText.text = f.Phase == FishingPhase.Ready ? "按住甩竿"
                : f.Phase == FishingPhase.Charging ? "松手甩出" : f.Phase == FishingPhase.Bite ? "提竿！"
                : f.Phase == FishingPhase.Fighting ? "按住收线 · 松开放线" : "等鱼咬钩…";
        }
        private void OpenFishBuyback(Window venue)
        {
            bool showAll = false;
            OpenWindow("鱼获回收", 760, 560, w =>
            {
                var tabs = Ui.Row(w.Content, 40);
                void Select(bool all) { showAll = all; w.ResetScrollOnRebuild = true; w.Rebuild(); }
                Ui.Size(Ui.Button(tabs, "背包鱼获", () => Select(false), 17, Ui.SelectedButton(!(showAll))), -1, 40, 1);
                Ui.Size(Ui.Button(tabs, "全部鱼种", () => Select(true), 17, Ui.SelectedButton(showAll)), -1, 40, 1);
                var list = Ui.Scroll(w.Content, 410);
                bool hasFish = false;
                foreach (var fish in FishingCatalog.Fish)
                {
                    int count = S.Backpack.Food.IngredientCount(fish.Id);
                    if (!showAll && count == 0) continue;
                    hasFish = true;
                    var row = Ui.Row(list, 60, 10);
                    ItemPortrait(row, fish.Id, 50);
                    string info = showAll
                        ? $"{fish.Rarity} · {new[] { "近岸", "中水", "远水" }[fish.Habitat]} · 偏好{FishingCatalog.Baits[fish.Bait]} · {fish.MovementName}"
                        : $"{fish.Kg:0.##} 千克/条 · 背包 {count} 条";
                    Ui.Size(ProductText(row, $"<b>{fish.Name}</b>\n<size=15>{info}</size>", 18), -1, -1, 1);
                    string price = Money.FromYuan(fish.SalePrice) + "／条";
                    Ui.Size(Ui.Label(row, price, 17, TextAnchor.MiddleRight, Ui.Good), 118);
                    if (count > 0)
                        Ui.Size(Ui.Button(row, "卖出 1 条", () =>
                        {
                            if (!S.SellFishingCatch(fish.Id, 1)) return;
                            venue.RequestRebuild();
                            w.Rebuild();
                        }, 16, Ui.ButtonBg), 108, 36);
                }
                if (!hasFish) Line(list, "背包里还没有鱼获。", 17, Ui.Dim);
            }, scrollContent: false);
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
