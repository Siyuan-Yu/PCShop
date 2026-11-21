using PCShopSell.Core.Session;
using PCShopSell.Game.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private RectTransform _footballCourt, _footballInputControls;
        private Text _footballScore, _footballPrompt, _footballPowerReadout;
        private Button _footballExit, _footballAimLeft, _footballAimRight, _footballCharge, _footballFinish;
        private Vector3 _footballMouse;

        private void BuildFootballPenalty(Window window, OnsitePlayState shot)
        {
            _ctrl.RenderOnsiteScene();
            _footballCourt = Ui.Rect(window.Content, "FootballCourt").Fill();
            var image = _footballCourt.gameObject.AddComponent<RawImage>();
            image.texture = _ctrl.OnsiteTexture;
            image.raycastTarget = false;
            _footballMouse = Input.mousePosition;

            var header = Ui.Panel(window.Content, "FootballHeader", Ui.Chrome).rectTransform;
            header.Anchor(.006f, .912f, .994f, .989f);
            Ui.Label(header, "社区运动场", 27, color: Ui.Accent).rectTransform.Anchor(.012f, 0, .13f, 1);
            Ui.Label(header, "点球练习", 21, color: Ui.Dim).rectTransform.Anchor(.108f, 0, .27f, 1);
            Ui.Label(header, "时间已暂停", 21, TextAnchor.MiddleCenter, Ui.Dim).rectTransform.Anchor(.40f, 0, .60f, 1);

            void Finish()
            {
                if (S.FinishOnsiteSport()) ReturnFromOnsitePlay();
            }

            _footballExit = Ui.Button(header, "提前结算", Finish, 21, Ui.ButtonBg);
            ((RectTransform)_footballExit.transform).Anchor(.904f, .18f, .988f, .82f);

            var score = Ui.Panel(window.Content, "FootballScore", Ui.Chrome).rectTransform;
            score.Anchor(.015f, .839f, .171f, .893f);
            _footballScore = Ui.Label(score, "", 21, TextAnchor.MiddleCenter, Ui.Accent);
            _footballScore.rectTransform.Fill(4);

            var footer = Ui.Panel(window.Content, "FootballControls", Ui.Chrome).rectTransform;
            footer.Anchor(.23f, .024f, .77f, .15f);
            _footballPrompt = Ui.Label(footer, "", 22, color: Ui.Accent);
            _footballPrompt.rectTransform.Anchor(.024f, .49f, .30f, .94f);
            _footballInputControls = Ui.Rect(footer, "FootballInputControls").Fill();
            _footballAimLeft = Ui.Button(_footballInputControls, "←", () => AimFootballStep(-1), 21, Ui.ButtonBg);
            _footballAimRight = Ui.Button(_footballInputControls, "→", () => AimFootballStep(1), 21, Ui.ButtonBg);
            ((RectTransform)_footballAimLeft.transform).Anchor(.30f, .57f, .35f, .84f);
            ((RectTransform)_footballAimRight.transform).Anchor(.359f, .57f, .409f, .84f);
            Ui.Label(_footballInputControls, "鼠标 / A、D / 左右键", 18, TextAnchor.MiddleRight, Ui.Dim)
                .rectTransform.Anchor(.43f, .53f, .685f, .88f);

            Ui.Label(_footballInputControls, "力度", 19, color: Ui.Dim).rectTransform.Anchor(.024f, .14f, .097f, .47f);
            var meter = Ui.Panel(_footballInputControls, "FootballPowerBackground", Ui.ChromeWell).rectTransform;
            meter.Anchor(.089f, .247f, .63f, .348f);
            _onsitePower = Ui.Panel(meter, "FootballPower", Ui.ButtonHot);
            _onsitePower.raycastTarget = false;
            _footballPowerReadout = Ui.Label(_footballInputControls, "", 18, TextAnchor.MiddleRight, Ui.Dim);
            _footballPowerReadout.rectTransform.Anchor(.632f, .14f, .685f, .47f);

            _footballCharge = Ui.Button(_footballInputControls, "按住蓄力，松手射门\n<size=16>鼠标或空格</size>", null, 24, Ui.ButtonHot);
            ((RectTransform)_footballCharge.transform).Anchor(.714f, .17f, .98f, .84f);
            _footballCharge.gameObject.AddComponent<FishingHoldInput>().Held = value => _onsiteHeld = value;
            _footballFinish = Ui.Button(footer, "结束并结算", Finish, 24, Ui.ButtonHot);
            ((RectTransform)_footballFinish.transform).Anchor(.714f, .17f, .98f, .84f);
            RefreshFootballPenalty(shot);
        }

        private void AimFootballStep(int direction)
        {
            var shot = S.CurrentOnsite;
            if (shot?.IsFootballPenalty == true) S.AimFootballPenalty(shot.FootballAimX + direction * .30);
        }

        private void UpdateFootballAim(OnsitePlayState shot)
        {
            Vector3 mouse = Input.mousePosition;
            bool moved = mouse != _footballMouse;
            _footballMouse = mouse;
            if (!shot.FootballPreparing || shot.Phase == "charging" || S.ManualAttemptsLeft == 0) return;
            if (DirectionalInput.LeftPressed) AimFootballStep(-1);
            else if (DirectionalInput.RightPressed) AimFootballStep(1);
            else if (moved && _footballCourt != null
                && RectTransformUtility.ScreenPointToLocalPointInRectangle(_footballCourt, mouse, null, out Vector2 point)
                && _footballCourt.rect.Contains(point))
                _ctrl.AimFootballPenalty(Rect.PointToNormalized(_footballCourt.rect, point));
        }

        private void RefreshFootballPenalty(OnsitePlayState shot)
        {
            if (_footballScore == null) return;
            bool finished = OnsiteReadyToFinish(S.RunningCityActivity);
            int ball = Mathf.Clamp(shot.FootballPreparing || shot.Phase == "flight" ? shot.Round + 1 : shot.Round,
                1, CityLifeCatalog.FootballPenaltyShots);
            string goals = $"<color=#B7D3A8>已进 {shot.Success} 球</color>";
            _footballScore.text = $"<b>第 {ball} / {CityLifeCatalog.FootballPenaltyShots} 球</b>　{goals}";
            _footballExit.gameObject.SetActive(!finished);
            _footballInputControls.gameObject.SetActive(!finished);
            _footballFinish.gameObject.SetActive(finished);
            if (finished)
            {
                _onsiteHeld = _onsiteWasHeld = false;
                _footballPrompt.text = $"本场已完成　{shot.Message}";
                _footballPrompt.rectTransform.Anchor(.024f, .17f, .69f, .84f);
                return;
            }

            _footballPrompt.text = shot.Phase switch
            {
                "ready" => "瞄准球门",
                "charging" => "正在蓄力",
                _ => shot.Message
            };
            _footballPowerReadout.text = $"{shot.Power:P0}";
            _onsitePower.rectTransform.Anchor(0, 0, (float)shot.Power, 1);

            bool canAim = shot.FootballPreparing && shot.Phase != "charging";
            _footballAimLeft.interactable = _footballAimRight.interactable = canAim;
            _footballCharge.interactable = shot.FootballPreparing;
            _footballExit.interactable = S.CanFinishOnsiteSport;
        }
    }
}
