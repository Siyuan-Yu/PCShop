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
            vh.Clear(); Quad(vh, 0, 0, 1, 1, new Color(.39f, .49f, .31f));
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
            var pivot = new Vector2(playerX + .012f, .407f);
            var tip = new Vector2(playerX + .055f, .48f);
            if (State?.Phase == FishingPhase.Charging) tip = new Vector2(playerX - .03f * (float)State.CastPower, .45f + .04f * (float)State.CastPower);
            Stroke(vh, pivot, tip, 4, new Color(.84f, .67f, .38f));
            if (State == null || State.Phase == FishingPhase.Ready || State.Phase == FishingPhase.Charging || State.Phase == FishingPhase.Ended) return;
            float flight = State.Phase == FishingPhase.Casting ? Mathf.Clamp01((float)State.PhaseSeconds / .8f) : 1;
            float distance = (float)State.CastDistance / 30;
            var landing = new Vector2(playerX + .07f + distance * .16f, .49f + distance * .29f);
            var bobber = Vector2.Lerp(tip, landing, flight);
            bobber.y += Mathf.Sin(flight * Mathf.PI) * .14f;
            if (State.Phase == FishingPhase.Waiting) bobber.y += Mathf.Sin(time * 3) * .002f;
            if (State.Phase == FishingPhase.Bite) bobber.y -= .007f + Mathf.Sin(time * 18) * .004f;
            if (State.Phase == FishingPhase.Fighting) bobber.x += ((float)State.FishPosition - .5f) * .07f;
            Stroke(vh, tip, bobber, 1.6f, new Color(.92f, .92f, .79f));
            Oval(vh, bobber.x, bobber.y - .007f, .012f + Mathf.Sin(time * 2) * .002f, .004f, new Color(.65f, .83f, .75f, .45f));
            Quad(vh, bobber.x - .002f, bobber.y - .008f, .004f, .016f, Ui.TextColor);
            Quad(vh, bobber.x - .002f, bobber.y, .004f, .008f, Ui.Warn);
            if (State.Phase == FishingPhase.Bite)
            { Quad(vh, bobber.x - .002f, bobber.y + .025f, .004f, .026f, Ui.Accent); Quad(vh, bobber.x - .002f, bobber.y + .015f, .004f, .005f, Ui.Accent); }
            if (State.Phase == FishingPhase.Fighting || State.Phase == FishingPhase.Landed)
            {
                float fx = State.Phase == FishingPhase.Landed ? playerX + .035f : bobber.x;
                float fy = State.Phase == FishingPhase.Landed ? .40f : bobber.y - .02f;
                Oval(vh, fx, fy, .011f, .005f, new Color(.66f, .77f, .65f));
                Stroke(vh, new Vector2(fx - .011f, fy), new Vector2(fx - .016f, fy + .006f), 3, new Color(.66f, .77f, .65f));
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
