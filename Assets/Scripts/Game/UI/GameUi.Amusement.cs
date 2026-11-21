using System;
using System.Linq;
using PCShopSell.Core.Session;
using PCShopSell.Game.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private Window _amusementWindow;
        public bool AmusementInputActive => _amusementWindow is { Closed: false };

        private bool TryOpenAmusementRide(CityActivity activity)
        {
            var ride = GameSession.RideAt(activity);
            if (ride == AmusementRide.None) return false;
            var session = S;
            var place = session.CurrentPlace;
            if (AmusementInputActive || place?.Id != activity.VenueId || _ctrl.Cam == null
                || _ctrl.World == null || !_ctrl.World.Places.TryGetValue(place.Id, out var venue)) return true;

            string title = session.CityActivityService(activity).Name;
            int seat = ride is AmusementRide.RollerCoaster or AmusementRide.Carousel ? -1 : 0;
            AmusementRideCamera view = null;
            _amusementWindow = OpenWindow(title, 0, 0, w =>
            {
                var header = Ui.Panel(w.Content, "RideHeader", Ui.Chrome).rectTransform.Anchor(0, .92f, 1, 1);
                Ui.Label(header, place.Name, 20).rectTransform.Anchor(.025f, 0, .30f, 1);
                Ui.Label(header, title, 28, TextAnchor.MiddleCenter, Ui.ChromeGold).rectTransform.Anchor(.30f, 0, .70f, 1);
                Ui.Label(header, $"{activity.Duration} 分钟", 18, TextAnchor.MiddleRight).rectTransform.Anchor(.70f, 0, .975f, 1);
                if (seat < 0)
                {
                    var card = Ui.Panel(w.Content, "RideSeatChoice", Ui.PanelBg).rectTransform.Anchor(.32f, .39f, .68f, .61f);
                    Ui.VList(card, 12, 18);
                    Line(card, "选一个座位", 24, Ui.Accent);
                    var choices = Ui.Row(card, 52, 8);
                    Ui.Size(Ui.Button(choices, ride == AmusementRide.RollerCoaster ? "坐前排" : "骑木马", () => Sit(0), 18, Ui.ButtonHot), 0, 48, 1);
                    Ui.Size(Ui.Button(choices, ride == AmusementRide.RollerCoaster ? "坐后排" : "坐马车", () => Sit(1), 18), 0, 48, 1);
                    return;
                }

                view ??= new AmusementRideCamera(venue, _ctrl.Cam, ride, seat);
                var viewport = Ui.Rect(w.Content, "RideViewport").Anchor(.023f, .112f, .977f, .91f);
                var stage = Ui.Rect(viewport, "RideCameraImage").Fill();
                var fit = stage.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                fit.aspectRatio = 16f / 9f;
                var raw = stage.gameObject.AddComponent<RawImage>();
                raw.raycastTarget = false;
                raw.texture = view.Texture;
                if (ride == AmusementRide.FerrisWheel) BuildCabinWindow(stage);

                var footer = Ui.Panel(w.Content, "RideControls", Ui.Chrome).rectTransform.Anchor(.05f, .025f, .95f, .10f);
                Action<float> step = ride == AmusementRide.RingToss
                    ? BuildRingTossControls(w, activity, stage, footer, view)
                    : BuildRideControls(w, activity, ride, footer, view);
                w.RefreshContent = () =>
                {
                    if (S != session || session.CurrentPlace != place || session.RunningCityActivity?.Id != activity.Id) { w.Close(); return; }
                    if (_windows.LastOrDefault() == w && Application.isFocused) step(Mathf.Min(Time.unscaledDeltaTime, .1f));
                };

                void Sit(int selected)
                {
                    seat = selected;
                    w.Rebuild();
                }
            }, closable: false, onClosed: () => { view?.Dispose(); _amusementWindow = null; },
                scrollContent: false, showTitleBar: false, fullScreen: true);
            return true;
        }

        private static void BuildCabinWindow(RectTransform stage)
        {
            Strip(0, 0, 1, .07f);
            Strip(0, .963f, 1, 1);
            Strip(0, .07f, .021f, .963f);
            Strip(.979f, .07f, 1, .963f);
            var sill = Ui.Panel(stage, "CabinWindowSill", ArtDirection.Hex("797D74"));
            sill.rectTransform.Anchor(.021f, .071f, .979f, .084f);
            sill.raycastTarget = false;

            void Strip(float xMin, float yMin, float xMax, float yMax)
            {
                var trim = Ui.Panel(stage, "CabinWindowTrim", Ui.Chrome);
                trim.raycastTarget = false;
                trim.rectTransform.Anchor(xMin, yMin, xMax, yMax);
            }
        }

        private Action<float> BuildRideControls(Window w, CityActivity activity, AmusementRide ride, RectTransform footer, AmusementRideCamera view)
        {
            var session = S;
            bool wheel = ride == AmusementRide.FerrisWheel;
            bool garden = activity.VenueId == CityLifeCatalog.GardenAmusementVenue;
            float duration = wheel ? 15f : ride == AmusementRide.RollerCoaster ? 10f : 8f;
            float elapsed = 0;
            bool talked = false;
            var companion = activity.Participants.FirstOrDefault(p => session.Relation(p.PersonId) >= 5);
            var name = companion == null ? null : session.CityPerson(companion.PersonId)?.Name;
            var hint = Ui.Label(footer, wheel ? "方向键 / WASD 或按住鼠标右键转头 · V 回正" : "乘坐中…", 18);
            hint.rectTransform.Anchor(.02f, 0, .69f, 1);
            var talk = Ui.Button(footer, name == null ? "" : "听" + name + "说说", () =>
            {
                if (talked || companion == null) return;
                talked = true;
                string id = companion.PersonId;
                string firstLine = garden ? "你看，赛道岛就在我们脚下。平时从岸边看，可分不清两座桥离得有多远。"
                    : "你看左边。那条贴着湖拐过去的路，就是我们刚才走过来的地方。";
                OpenDialogue(id, name, session.CityPersonRole(id), "", null, start: ctx => ctx.Show(firstLine,
                    new TalkOpt { Label = "你最常去哪边？", Act = () => ctx.Show(
                        session.CityTalk(id, "life") ?? "我喜欢沿湖走到人少的地方，在水边坐一会儿。", Back(ctx)) },
                    Back(ctx)));
            }, 17, Ui.ButtonHot);
            talk.GetComponent<RectTransform>().Anchor(.74f, .15f, .98f, .85f);
            talk.gameObject.SetActive(false);

            TalkOpt Back(TalkCtx ctx) => new TalkOpt { Label = "继续看风景", Hot = true, Act = () => ctx.W.Close() };

            return dt =>
            {
                elapsed = Mathf.Min(duration, elapsed + dt);
                if (wheel)
                {
                    var keys = DirectionalInput.Held;
                    view.Look(new Vector2(keys.x, -keys.y) * (dt * 48));
                    if (Input.GetMouseButton(1)) view.Look(new Vector2(Input.GetAxisRaw("Mouse X"), -Input.GetAxisRaw("Mouse Y")) * 2.2f);
                    if (Input.GetKeyDown(KeyCode.V)) view.ResetLook();
                }
                float progress = elapsed / duration;
                if (wheel)
                {
                    // Ease through the highest point while leaving the whole city under player control.
                    progress = elapsed <= 7.5f ? .5f * Mathf.SmoothStep(0, 1, elapsed / 7.5f)
                        : .5f + .5f * Mathf.SmoothStep(0, 1, (elapsed - 7.5f) / 7.5f);
                    bool atTop = name != null && !talked && elapsed >= 5.5f && elapsed <= 10.5f;
                    talk.gameObject.SetActive(atTop);
                    hint.text = atTop ? name + "：你看那边。　·　方向键 / WASD 转头"
                        : "方向键 / WASD 或按住鼠标右键转头 · V 回正";
                }
                else if (ride == AmusementRide.RollerCoaster) progress -= .095f * Mathf.Sin(progress * Mathf.PI * 2);
                view.Render(progress);
                if (elapsed >= duration) FinishAmusementRide(w, activity);
            };
        }

        private Action<float> BuildRingTossControls(Window w, CityActivity activity, RectTransform stage, RectTransform footer, AmusementRideCamera view)
        {
            var session = S;
            float aim = 0, power = 0, meter = 0, flight = 1.3f, launchAim = 0, launchPower = 0;
            bool hit = false;
            var reticle = Ui.Label(stage, "◯", 37, TextAnchor.MiddleCenter, Ui.ChromeGold);
            reticle.raycastTarget = false;
            var status = Ui.Label(footer, "", 17);
            status.rectTransform.Anchor(.018f, 0, .47f, 1);
            var bar = Ui.Panel(footer, "ThrowPower", Ui.ChromeButton).rectTransform.Anchor(.48f, .28f, .71f, .70f);
            var fill = Ui.Panel(bar, "ThrowPowerFill", Ui.ChromeGold).rectTransform;
            var throwers = new[] { "你" }.Concat(activity.Participants.Select(p => session.CityPerson(p.PersonId)?.Name ?? "同行者")).ToArray();
            var button = Ui.Button(footer, "空格 / 点击投圈", Throw, 18, Ui.ButtonHot);
            var buttonLabel = button.GetComponentInChildren<Text>();
            button.GetComponent<RectTransform>().Anchor(.76f, .12f, .982f, .88f);

            void Throw()
            {
                if (flight < 1.3f || _windows.LastOrDefault() != w) return;
                if (GameSession.RingThrows(activity) % throwers.Length == 0)
                {
                    if (session.ThrowParkRing(activity.Id, aim, power) is not bool result) return;
                    launchAim = aim;
                    launchPower = power;
                    hit = result;
                }
                else
                {
                    var result = session.ThrowCompanionParkRing(activity.Id);
                    if (!result.HasValue) return;
                    launchAim = (float)result.Value.Aim;
                    launchPower = (float)result.Value.Power;
                    hit = result.Value.Hit;
                }
                flight = 0;
            }

            return dt =>
            {
                view.Render(0);
                int used = GameSession.RingThrows(activity);
                int turn = used % throwers.Length;
                flight = Mathf.Min(1.3f, flight + dt);
                if (used == GameSession.RingsPerRound && flight >= 1.3f) { FinishAmusementRide(w, activity); return; }
                if (flight >= 1.3f)
                {
                    if (turn == 0)
                    {
                        aim = Mathf.Clamp(aim + DirectionalInput.Horizontal * dt * 1.25f, -.87f, .87f);
                        meter += dt;
                        power = Mathf.PingPong(meter * .85f, 1);
                    }
                    if (Input.GetKeyDown(KeyCode.Space)) Throw();
                }
                bool throwing = flight < 1.3f;
                float t = Mathf.Clamp01(flight / .58f);
                Vector2 end = view.RingViewport(throwing ? launchAim : aim, throwing ? launchPower : .6f);
                Vector2 target = throwing ? Vector2.Lerp(new Vector2(.5f, .14f), end, t) + Vector2.up * (.23f * Mathf.Sin(t * Mathf.PI)) : end;
                reticle.rectTransform.Anchor(target.x, target.y, target.x, target.y, -24, -24, -24, -24);
                reticle.color = throwing && t >= 1 ? (hit ? Ui.ChromeGood : Ui.ChromeWarn) : Ui.ChromeGold;
                fill.Anchor(0, 0, power, 1);
                status.text = throwing ? t < 1 ? "圈在空中…" : hit ? "套中了！" : "这圈没套中。"
                    : $"轮到{throwers[turn]} · 剩 {GameSession.RingsPerRound - used} 圈" + (turn == 0 ? " · A/D 或 ←/→ 瞄准" : "");
                reticle.enabled = throwing || turn == 0;
                bar.gameObject.SetActive(!throwing && turn == 0);
                buttonLabel.text = turn == 0 ? "空格 / 点击投圈" : $"让{throwers[turn]}投";
                button.interactable = !throwing;
            };
        }

        private void FinishAmusementRide(Window window, CityActivity activity)
        {
            var session = S;
            var place = session.CurrentPlace;
            bool finished;
            _finishingActivityCgId = activity.Id;
            try { finished = session.FinishManualCityActivity(activity.Id); }
            finally { _finishingActivityCgId = null; }
            if (!finished) return;
            window.Close();
            if (S != session || session.CurrentPlace != place || session.IsGameOver || AnyModalOpen) return;
            OpenCityVenue(place);
            if (activity.EventSettlementNote != null) Toast(activity.EventSettlementNote);
            if (GameSession.RideAt(activity) != AmusementRide.RollerCoaster || activity.State != CityActivityPhase.Completed) return;
            string id = activity.Participants.FirstOrDefault()?.PersonId;
            var friend = id == null ? null : session.CityPerson(id);
            if (friend == null || !session.CitySamePlace(id)) return;
            string reply = session.CityPersonAttributes(id).Stamina >= 75 ? "刚才那个下坡真过瘾！下次还坐这一车。"
                : "刚到坡顶我就把眼睛闭上了。咱们先歇会儿，下一个挑个慢一点的吧。";
            OpenDialogue(id, friend.Name, session.CityPersonRole(id), "", null, firstReply: reply);
        }
    }
}
