using PCShopSell.Core.Session;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    // Code-native, animated water/rod/float diagram. Positions reflect the actual cast state.
    [RequireComponent(typeof(CanvasRenderer))]
    public class FishingWaterGraphic : MaskableGraphic
    {
        public FishingState State;
        public bool Illustrated;
        private static readonly float[] Piers = { .232f, .490f, .715f };
        private static readonly float[] PierHeights = { .320f, .333f, .352f };

        public float DockCenter(int spot) => Illustrated ? Piers[spot] : .289f + spot * .17f;

        private Vector2 RodBase
        {
            get
            {
                int spot = State?.Spot ?? 0;
                return Illustrated ? new Vector2(DockCenter(spot), PierHeights[spot])
                    : new Vector2(DockCenter(spot) + .012f, .407f);
            }
        }

        private Vector2 RodTip
        {
            get
            {
                var pivot = RodBase;
                if (State?.Phase == FishingPhase.Charging)
                    return pivot + new Vector2(-.042f * (float)State.CastPower, .114f);
                return pivot + (Illustrated
                    ? new Vector2(State?.Spot == 2 ? -.042f : .049f, .161f)
                    : new Vector2(.043f, .073f));
            }
        }

        public Vector2 BobberPosition
        {
            get
            {
                if (State == null) return RodTip;
                float time = (float)(State.PhaseSeconds + State.FightSeconds);
                float flight = State.Phase == FishingPhase.Casting ? Mathf.Clamp01((float)State.PhaseSeconds / .8f) : 1;
                float distance = Mathf.Clamp01((float)State.CastDistance / 30);
                float x = DockCenter(State.Spot);
                var landing = Illustrated
                    ? new Vector2(x + (.055f + distance * .085f) * (State.Spot == 2 ? -1 : 1), .392f + distance * .164f)
                    : new Vector2(x + .07f + distance * .16f, .49f + distance * .29f);
                var bobber = Vector2.Lerp(RodTip, landing, flight);
                bobber.y += Mathf.Sin(flight * Mathf.PI) * .14f;
                if (State.Phase == FishingPhase.Waiting) bobber.y += Mathf.Sin(time * 3) * .003f;
                if (State.Phase == FishingPhase.Bite) bobber.y -= .012f + Mathf.Sin(time * 18) * .008f;
                if (State.Phase == FishingPhase.Fighting) bobber.x += ((float)State.FishPosition - .5f) * .09f;
                return bobber;
            }
        }
        protected Vector2 P(float x, float y) => new Vector2(rectTransform.rect.xMin + rectTransform.rect.width * x, rectTransform.rect.yMin + rectTransform.rect.height * y);
        protected void Quad(VertexHelper vh, float x, float y, float w, float h, Color c)
        {
            int n = vh.currentVertCount;
            vh.AddVert(P(x, y), c, Vector2.zero); vh.AddVert(P(x + w, y), c, Vector2.zero);
            vh.AddVert(P(x + w, y + h), c, Vector2.zero); vh.AddVert(P(x, y + h), c, Vector2.zero);
            vh.AddTriangle(n, n + 1, n + 2); vh.AddTriangle(n, n + 2, n + 3);
        }
        protected void Stroke(VertexHelper vh, Vector2 a, Vector2 b, float width, Color c)
        {
            a = P(a.x, a.y); b = P(b.x, b.y); Vector2 normal = new Vector2(-(b - a).y, (b - a).x).normalized * width / 2;
            int n = vh.currentVertCount;
            vh.AddVert(a - normal, c, Vector2.zero); vh.AddVert(b - normal, c, Vector2.zero);
            vh.AddVert(b + normal, c, Vector2.zero); vh.AddVert(a + normal, c, Vector2.zero);
            vh.AddTriangle(n, n + 1, n + 2); vh.AddTriangle(n, n + 2, n + 3);
        }
        private void Oval(VertexHelper vh, float x, float y, float rx, float ry, Color c, float wobble = 0)
        {
            int n = vh.currentVertCount; vh.AddVert(P(x, y), c, Vector2.zero);
            const int count = 64;
            for (int i = 0; i <= count; i++)
            {
                float angle = i * Mathf.PI * 2 / count;
                float wave = 1 + wobble * Mathf.Sin(angle * 5 + .3f) + wobble * .4f * Mathf.Cos(angle * 9);
                vh.AddVert(P(x + Mathf.Cos(angle) * rx * wave, y + Mathf.Sin(angle) * ry * wave), c, Vector2.zero);
                if (i > 0) vh.AddTriangle(n, n + i, n + i + 1);
            }
        }
        private void Tree(VertexHelper vh, float x, float y, float size)
        {
            Oval(vh, x + .009f, y - .009f, .030f * size, .036f * size, new Color(.14f, .22f, .14f, .5f));
            Quad(vh, x - .004f, y - .045f * size, .008f, .042f * size, new Color(.30f, .23f, .15f));
            Oval(vh, x, y, .026f * size, .043f * size, new Color(.19f, .34f, .23f));
            Oval(vh, x - .007f, y + .009f, .021f * size, .035f * size, new Color(.28f, .43f, .28f));
            Oval(vh, x - .01f, y + .018f, .013f * size, .022f * size, new Color(.37f, .51f, .32f));
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (Illustrated)
            {
                DrawRodAndFloat(vh);
                return;
            }
            Quad(vh, 0, 0, 1, 1, new Color(.39f, .49f, .31f));
            for (int i = 0; i < 75; i++)
            {
                float x = (i * .137f) % 1, y = .23f + (i * .173f) % .77f;
                Stroke(vh, new Vector2(x, y), new Vector2(x + .004f, y + .008f), 2, new Color(.51f, .59f, .37f, .4f));
            }
            Oval(vh, .54f, .56f, .468f, .34f, new Color(.65f, .64f, .43f), .025f);
            Oval(vh, .54f, .568f, .45f, .32f, new Color(.24f, .49f, .46f), .025f);
            Oval(vh, .555f, .584f, .405f, .285f, new Color(.18f, .43f, .44f), .02f);
            Oval(vh, .58f, .61f, .29f, .21f, new Color(.14f, .36f, .40f), .015f);
            float time = State == null ? 0 : (float)(State.PhaseSeconds + State.FightSeconds);
            for (int i = 0; i < 45; i++)
            {
                float angle = i * 2.4f, radial = .2f + (i % 7) * .09f;
                float x = .54f + Mathf.Cos(angle) * .42f * radial, y = .57f + Mathf.Sin(angle) * .29f * radial;
                float slide = Mathf.Sin(time * .7f + i) * .003f;
                Stroke(vh, new Vector2(x + slide, y), new Vector2(x + .018f + slide, y + .002f), 1.3f, new Color(.55f, .73f, .67f, .35f));
            }
            for (int i = 0; i < 7; i++)
            {
                float x = .13f + i * .125f;
                Tree(vh, x, .90f + Mathf.Sin(i * 2) * .023f, 1.25f);
            }
            Tree(vh, .065f, .69f, 1.8f); Tree(vh, .07f, .47f, 1.6f); Tree(vh, .93f, .33f, 1.2f);
            for (int i = 0; i < 18; i++)
            {
                float x = .12f + (i % 6) * .012f, y = .39f + (i / 6) * .03f;
                Stroke(vh, new Vector2(x, y), new Vector2(x - .004f, y + .036f), 3, new Color(.32f, .43f, .24f));
                Stroke(vh, new Vector2(x, y + .03f), new Vector2(x - .005f, y + .05f), 4, new Color(.57f, .46f, .28f));
            }
            for (int dock = 0; dock < 3; dock++)
            {
                float x = .26f + dock * .17f;
                Quad(vh, x + .008f, .245f, .067f, .19f, new Color(.08f, .20f, .20f, .5f));
                Quad(vh, x, .25f, .06f, .185f, new Color(.53f, .40f, .26f));
                for (int j = 0; j < 10; j++) Stroke(vh, new Vector2(x, .25f + j * .018f), new Vector2(x + .06f, .25f + j * .018f), 1.5f, new Color(.33f, .26f, .18f));
                Quad(vh, x - .003f, .427f, .009f, .018f, new Color(.65f, .52f, .34f));
                Quad(vh, x + .054f, .427f, .009f, .018f, new Color(.65f, .52f, .34f));
            }
            float playerX = .289f + (State?.Spot ?? 0) * .17f;
            Oval(vh, playerX + .005f, .362f, .017f, .010f, new Color(.11f, .18f, .17f, .6f));
            Quad(vh, playerX - .009f, .365f, .007f, .024f, new Color(.17f, .21f, .24f));
            Quad(vh, playerX + .003f, .365f, .007f, .024f, new Color(.17f, .21f, .24f));
            Quad(vh, playerX - .013f, .383f, .026f, .032f, new Color(.65f, .35f, .22f));
            Oval(vh, playerX, .425f, .009f, .015f, new Color(.86f, .70f, .48f));
            Oval(vh, playerX, .434f, .014f, .007f, new Color(.78f, .65f, .39f));
            Quad(vh, playerX + .028f, .365f, .015f, .024f, new Color(.32f, .40f, .42f));
            DrawRodAndFloat(vh);
        }

        private void DrawRodAndFloat(VertexHelper vh)
        {
            var tip = RodTip;
            var pivot = RodBase;
            if (Illustrated && State?.Phase == FishingPhase.Ended) return;
            Stroke(vh, pivot, tip, Illustrated ? 5 : 4, new Color(.29f, .19f, .13f));
            Stroke(vh, Vector2.Lerp(pivot, tip, .16f), tip, 2, new Color(.85f, .69f, .44f));
            if (State == null || State.Phase is FishingPhase.Ready or FishingPhase.Charging or FishingPhase.Ended) return;

            var bobber = BobberPosition;
            Stroke(vh, tip, bobber, 2, new Color(.12f, .30f, .30f, .64f));
            Stroke(vh, tip, bobber, 1, new Color(.94f, .96f, .83f, .84f));
            bool biting = State.Phase == FishingPhase.Bite;
            if (State.Phase != FishingPhase.Casting)
            {
                float time = (float)State.PhaseSeconds;
                int rings = biting ? 3 : 1;
                for (int ring = 0; ring < rings; ring++)
                {
                    float wave = Mathf.Repeat(time * (biting ? 1.9f : .9f) + ring / 3f, 1);
                    float radius = (biting ? .018f : .008f) + wave * (biting ? .047f : .018f);
                    var tint = new Color(.96f, 1f, .89f, (1 - wave) * (biting ? .88f : .47f));
                    var previous = bobber + new Vector2(radius, 0);
                    for (int segment = 1; segment <= 32; segment++)
                    {
                        float angle = segment * Mathf.PI * 2 / 32;
                        var point = bobber + new Vector2(radius * Mathf.Cos(angle), radius * .53f * Mathf.Sin(angle));
                        Stroke(vh, previous, point, biting ? 2.2f : 1.25f, tint);
                        previous = point;
                    }
                }
            }
            float top = biting ? .005f : .017f;
            Oval(vh, bobber.x, bobber.y - .003f, .007f, .003f, new Color(.11f, .29f, .31f, .6f));
            Stroke(vh, bobber - new Vector2(0, .004f), bobber + new Vector2(-.003f, top), 9, new Color(.22f, .28f, .25f));
            Stroke(vh, bobber, bobber + new Vector2(-.003f, top), 6, new Color(1f, .95f, .70f));
            Stroke(vh, bobber + new Vector2(-.0014f, top * .45f), bobber + new Vector2(-.003f, top), 6,
                new Color(.95f, .36f, .16f));
            if (State.Phase is FishingPhase.Fighting or FishingPhase.Landed)
            {
                float fx = State.Phase == FishingPhase.Landed ? pivot.x + .035f : bobber.x;
                float fy = State.Phase == FishingPhase.Landed ? pivot.y : bobber.y - .02f;
                var fish = new Color(.65f, .84f, .68f, .76f);
                Oval(vh, fx, fy, .011f, .005f, fish);
                Stroke(vh, new Vector2(fx - .011f, fy), new Vector2(fx - .016f, fy + .006f), 3, fish);
            }
        }
    }
    public sealed class FishingFightGraphic : FishingWaterGraphic
    {
        public float ZoneSize;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); Quad(vh, 0, 0, 1, 1, Ui.ButtonBg);
            Quad(vh, .12f, .03f, .76f, .94f, new Color(.07f, .17f, .24f));
            if (State == null) return;
            float y = .03f + .94f * ((float)State.ControlPosition - ZoneSize / 2);
            Quad(vh, .17f, y, .66f, ZoneSize * .94f, new Color(.4f, .68f, .35f));
            float fish = .03f + .94f * (float)State.FishPosition;
            Quad(vh, .26f, fish - .016f, .46f, .032f, Ui.TextColor);
            Stroke(vh, new Vector2(.26f, fish), new Vector2(.18f, fish + .028f), 3, Ui.TextColor);
            Stroke(vh, new Vector2(.26f, fish), new Vector2(.18f, fish - .028f), 3, Ui.TextColor);
        }
    }
}
