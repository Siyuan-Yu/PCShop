using System.Linq;
using PCShopSell.Core.Session;
using PCShopSell.Game.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private Window _parkRowingWindow;

        private void OpenParkRowing()
        {
            if (!S.ParkRowingActive || (_parkRowingWindow != null && !_parkRowingWindow.Closed)) return;
            CloseAllWindows();
            _parkRowingWindow = OpenWindow("宜居社区公园 · 划船", 1920, 1080, w =>
            {
                var texture = Resources.Load<Texture2D>("EventCg/community_park_rowing");
                var stage = Ui.Rect(w.Content, "RowingLake").Fill();
                var fitter = stage.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                fitter.aspectRatio = texture == null ? 16f / 9f : (float)texture.width / texture.height;
                if (texture != null)
                {
                    var background = Ui.Rect(stage, "Pond").Fill().gameObject.AddComponent<RawImage>();
                    background.texture = texture;
                    background.raycastTarget = false;
                }
                var boat = Ui.Rect(stage, "RowboatAndWake").Fill().gameObject.AddComponent<ParkRowingGraphic>();
                boat.raycastTarget = false;
                boat.PaintedPond = texture != null;

                void DockOrFinish()
                {
                    if (S.ParkRowing?.AtDock == true)
                    {
                        if (S.FinishParkRowing()) w.Close();
                    }
                    else S.ReturnParkRowingToDock();
                }

                var header = BuildLeisureHeader(w, "宜居社区公园 · 划船", "收桨靠岸", DockOrFinish);
                var dock = header.GetComponentInChildren<Button>();
                var dockText = dock.GetComponentInChildren<Text>();
                var panel = Ui.FlatPanel(w.Content, "RowingControls", Ui.PanelBg).rectTransform
                    .Anchor(.31f, .026f, .69f, .164f);
                var heading = Ui.Label(panel, "", 19, TextAnchor.MiddleCenter, Ui.Accent);
                heading.rectTransform.Anchor(.03f, .72f, .97f, .97f);
                var lane = Ui.Panel(panel, "RowingRhythm", Ui.ChromeButton);
                lane.rectTransform.Anchor(.18f, .57f, .82f, .67f);
                var zone = Ui.Panel(lane.transform, "SmoothStrokeWindow", ArtDirection.Hex("90AB78"));
                zone.rectTransform.Anchor((float)GameSession.ParkRowingCueStart, 0, (float)GameSession.ParkRowingCueEnd, 1);
                var needle = Ui.Panel(lane.transform, "RowingBeat", Ui.ChromeGold);
                var left = Ui.Button(panel, "← / A　左桨", () => S.StrokeParkRowing(false), 19, Ui.ButtonHot);
                left.GetComponent<RectTransform>().Anchor(.027f, .10f, .49f, .49f);
                var right = Ui.Button(panel, "右桨　D / →", () => S.StrokeParkRowing(true), 19);
                right.GetComponent<RectTransform>().Anchor(.51f, .10f, .973f, .49f);
                var friend = S.RunningCityActivity?.Participants.FirstOrDefault(p => p.Phase == CityParticipantPhase.Participating);
                if (friend != null)
                {
                    boat.HasCompanion = true;
                    var companion = Ui.FlatPanel(w.Content, "RowingCompanion", Ui.PanelBg).rectTransform
                        .Anchor(.024f, .045f, .285f, .139f);
                    var portrait = Ui.Rect(companion, "CompanionPortrait").Anchor(0, 0, .18f, 1, 6, 6, 6, 6);
                    FitPortrait(portrait, friend.PersonId, 2);
                    string person = S.CityPerson(friend.PersonId)?.Name ?? "同行者";
                    string words = S.IsPlayerPartner(friend.PersonId) ? "慢慢划吧，我还想跟你多待一会儿。"
                        : "不着急。我们边划边看看两岸。";
                    Ui.Label(companion, $"<b>{person}</b>\n{words}", 17).rectTransform.Anchor(.19f, .05f, .97f, .95f);
                }

                w.RefreshContent = () =>
                {
                    var state = S.ParkRowing;
                    if (state == null) { w.Close(); return; }
                    if (_windows.LastOrDefault() == w && Application.isFocused)
                    {
                        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) S.StrokeParkRowing(false);
                        else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) S.StrokeParkRowing(true);
                        S.StepParkRowing(Time.unscaledDeltaTime);
                        state = S.ParkRowing;
                        if (state == null) { w.Close(); return; }
                    }
                    boat.State = state;
                    boat.SetVerticesDirty();
                    bool canStroke = !state.AtDock && !state.Returning;
                    float phase = (float)(state.BeatElapsed / GameSession.ParkRowingBeatSeconds);
                    bool cue = canStroke && state.StrokeCooldown <= 0
                        && phase >= GameSession.ParkRowingCueStart && phase <= GameSession.ParkRowingCueEnd;
                    needle.rectTransform.Anchor(phase, 0, phase, 1, -3, -2, -3, -2);
                    lane.gameObject.SetActive(canStroke);
                    left.interactable = right.interactable = canStroke;
                    Ui.SetButtonFill(left, cue && !state.NextRight ? Ui.ButtonHot : Ui.ButtonBg);
                    Ui.SetButtonFill(right, cue && state.NextRight ? Ui.ButtonHot : Ui.ButtonBg);
                    string next = state.NextRight ? "下一桨：右桨" : "下一桨：左桨";
                    heading.text = canStroke ? string.IsNullOrEmpty(state.Reply) ? next : next + "　·　" + state.Reply
                        : state.Reply;
                    dockText.text = state.AtDock ? "上岸" : state.Returning ? "正在靠岸" : "收桨靠岸";
                    dock.interactable = state.AtDock || !state.Returning;
                };
                w.RefreshContent();
            }, holds: false, closable: false, onClosed: () => _parkRowingWindow = null,
                scrollContent: false, showTitleBar: false, fullScreen: true);
        }
    }

    public sealed class ParkRowingGraphic : MaskableGraphic
    {
        public ParkRowingState State;
        public bool PaintedPond, HasCompanion;

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = rectTransform.rect;
            if (rect.width <= 0 || rect.height <= 0) return;
            if (!PaintedPond)
            {
                Poly(mesh, ArtDirection.Hex("80946D"), rect.min, new Vector2(rect.xMax, rect.yMin), rect.max, new Vector2(rect.xMin, rect.yMax));
                Oval(mesh, rect.center, new Vector2(rect.width * .35f, 0), new Vector2(0, rect.height * .37f), ArtDirection.Hex("5C8D87"));
            }

            if (State == null) return;
            bool reversing = State.Returning && State.ReturnTarget == 0 && !State.AtDock;
            float theta = (float)(State.Position * Mathf.PI * 2);
            var pos = new Vector2(.50f + Mathf.Sin(theta) * .28f, .514f - Mathf.Cos(theta) * .226f);
            var center = rect.min + Vector2.Scale(pos, rect.size);
            var direction = new Vector2(Mathf.Cos(theta) * rect.width * .28f,
                Mathf.Sin(theta) * rect.height * .226f).normalized * (reversing ? -1 : 1);
            var cross = new Vector2(direction.y, -direction.x);
            float unit = Mathf.Min(rect.width, rect.height) * .047f;
            Vector2 P(float x, float y) => center + cross * (unit * x) + direction * (unit * y);

            if (!State.AtDock && (State.Speed > .004 || State.Returning))
            {
                float strength = State.Returning ? .50f : Mathf.Clamp01((float)(State.Speed * 12));
                for (int ring = 0; ring < 3; ring++)
                {
                    float spread = .44f + ring * .25f, back = -1.02f - ring * .27f;
                    Color foam = new Color(.86f, .97f, .88f, strength * (.55f - ring * .12f));
                    Line(mesh, P(-spread, back + .22f), P(0, back), 2.3f, foam);
                    Line(mesh, P(0, back), P(spread, back + .22f), 2.3f, foam);
                }
            }

            Oval(mesh, center + new Vector2(3, -4), cross * unit * .55f, direction * unit * 1.16f, new Color(.11f, .20f, .17f, .51f));
            Poly(mesh, ArtDirection.Hex("66432D"), P(0, 1.13f), P(.36f, .79f), P(.52f, .30f), P(.50f, -.69f),
                P(.28f, -.96f), P(-.28f, -.96f), P(-.50f, -.69f), P(-.52f, .30f), P(-.36f, .79f));
            Poly(mesh, ArtDirection.Hex("DBAB72"), P(0, .94f), P(.29f, .64f), P(.42f, .26f), P(.39f, -.61f),
                P(.26f, -.83f), P(-.26f, -.83f), P(-.39f, -.61f), P(-.42f, .26f), P(-.29f, .64f));
            Poly(mesh, ArtDirection.Hex("AF7A50"), P(-.37f, .20f), P(.37f, .20f), P(.37f, .08f), P(-.37f, .08f));
            Line(mesh, P(-.30f, -.44f), P(.30f, -.44f), unit * .155f, ArtDirection.Cream);
            Line(mesh, P(-.32f, .53f), P(.32f, .53f), unit * .13f, ArtDirection.Cream);
            Oval(mesh, P(0, -.33f), cross * unit * .15f, direction * unit * .17f, ArtDirection.Sage);
            if (HasCompanion) Oval(mesh, P(0, .46f), cross * unit * .14f, direction * unit * .16f, ArtDirection.Clay);

            for (int side = -1; side <= 1; side += 2)
            {
                bool justUsed = !State.Returning && State.StrokeCooldown > 0 && State.NextRight == (side < 0);
                float swing = justUsed ? Mathf.Sin((float)State.StrokeCooldown / .32f * Mathf.PI) : 0;
                float reach = .76f + swing * .38f;
                float tip = -.21f - swing * .57f;
                Line(mesh, P(side * .13f, .01f), P(side * reach, tip), unit * .066f, ArtDirection.Hex("976A42"));
                Line(mesh, P(side * reach, tip), P(side * (reach + .18f), tip - .075f), unit * .14f, ArtDirection.Hex("D4A66F"));
                if (justUsed)
                {
                    Color splash = new Color(.92f, 1, .93f, swing * .57f);
                    Oval(mesh, P(side * (reach + .15f), tip), cross * (unit * .19f), direction * (unit * .24f), splash);
                }
            }
        }

        private static void Poly(VertexHelper mesh, Color color, params Vector2[] points)
        {
            int start = mesh.currentVertCount;
            foreach (var point in points) mesh.AddVert(point, color, Vector2.zero);
            for (int i = 2; i < points.Length; i++) mesh.AddTriangle(start, start + i - 1, start + i);
        }

        private static void Line(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color color)
        {
            Vector2 delta = b - a;
            var normal = new Vector2(-delta.y, delta.x).normalized * (width / 2);
            Poly(mesh, color, a - normal, a + normal, b + normal, b - normal);
        }

        private static void Oval(VertexHelper mesh, Vector2 center, Vector2 x, Vector2 y, Color color)
        {
            const int sides = 32;
            int start = mesh.currentVertCount;
            mesh.AddVert(center, color, Vector2.zero);
            for (int i = 0; i <= sides; i++)
            {
                float angle = i * Mathf.PI * 2 / sides;
                mesh.AddVert(center + x * Mathf.Cos(angle) + y * Mathf.Sin(angle), color, Vector2.zero);
                if (i > 0) mesh.AddTriangle(start, start + i, start + i + 1);
            }
        }
    }
}
