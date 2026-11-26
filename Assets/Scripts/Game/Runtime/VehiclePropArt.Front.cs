using System;
using UnityEngine;
using static PCShopSell.Game.Runtime.PropForms;

namespace PCShopSell.Game.Runtime
{
    internal static partial class VehiclePropArt
    {
        private static void BuildCutNose(Transform root, string key, PassengerShape shape,
            Material paint, Material paintedEdge, Material shadow)
        {
            bool wedge = shape.IsWedge;
            var ductWall = Mats.Lit(ArtDirection.Hex(wedge ? "383B37" : "313230"), .22f);
            var ductBack = Mats.Lit(ArtDirection.Hex("202522"), .11f);
            var lip = Mats.LitTex(ArtDirection.Hex(wedge ? "373A33" : "3C3937"), new Vector2(2, 1), .28f);
            var lipEdge = Mats.Lit(ArtDirection.Hex(wedge ? "5B5C4F" : "514E48"), .26f);
            float lipExtension = wedge ? .079f : .053f;

            Vector3 Top(float v) => shape.BodyPanel(1, v);
            Vector3 Profile(float v, float height, float forward)
            {
                Vector3 top = Top(v);
                float a = Mathf.Abs(v * 2f - 1f);
                float fade = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.828f, 1, a));
                float floor = .166f + .024f * a;
                float y = Mathf.Lerp(Mathf.Min(top.y, floor), top.y, height);
                float extension = forward * fade * (wedge ? 1 - .15f * a : 1 - .23f * a);
                return new Vector3(top.x, y, top.z + extension);
            }

            Vector3 Brow(float v) => Profile(v, wedge ? .80f : .73f, wedge ? .021f : .014f);
            Vector3 VentBottom(float v) => Profile(v, wedge ? .30f : .28f, lipExtension * .64f);
            Vector3 LipEdge(float v) => Profile(v, .16f, lipExtension);
            Vector3 UnderLip(float v) => Profile(v, .025f, lipExtension - .014f);
            CarSurface brow = (u, v) => Vector3.Lerp(Brow(v), Top(v), u);
            Patch(root, key, "引擎盖下折面", paint, brow, 5, 44, flip: true,
                normalAt: (u, v) => Vector3.Lerp(SurfaceNormal(brow, u, v),
                    -SurfaceNormal(shape.BodyPanel, 1, v), u * .72f).normalized);
            Patch(root, key, "前唇上斜面", lip, (u, v) => Vector3.Lerp(LipEdge(v), VentBottom(v), u),
                3, 44, flip: true);
            Patch(root, key, "前唇迎风面", lipEdge, (u, v) => Vector3.Lerp(UnderLip(v), LipEdge(v), u),
                3, 44, flip: true);
            Patch(root, key, "前唇底板", ductWall, (u, v) => Vector3.Lerp(Profile(v, .025f, -.086f), UnderLip(v), u),
                2, 44, flip: true);

            // Each pair sets the lower and upper width of an opening or painted support.
            var divisions = wedge
                ? new[] { Vector2.zero, new Vector2(.344f, .292f), new Vector2(.434f, .427f), new Vector2(.814f, .925f), Vector2.one }
                : new[] { Vector2.zero, new Vector2(.305f, .345f), new Vector2(.475f, .501f), new Vector2(.869f, .914f), Vector2.one };
            foreach (int side in new[] { -1, 1 })
            for (int band = 0; band < divisions.Length - 1; band++)
            {
                Vector2 first = divisions[band], last = divisions[band + 1];
                int facing = side;
                string id = side + ":" + band;
                Vector3 Mouth(float u, float v)
                {
                    float across = Mathf.Lerp(Mathf.Lerp(first.x, first.y, u), Mathf.Lerp(last.x, last.y, u), v);
                    float at = .5f + facing * across * .5f;
                    return Vector3.Lerp(VentBottom(at), Brow(at), u);
                }

                bool flip = side > 0;
                if (band == 1 || band == 3)
                {
                    Patch(root, key, "保险杠折角" + id, band == 1 && wedge ? paintedEdge : paint,
                        Mouth, 4, 8, flip: flip);
                    continue;
                }

                float depth = band == 0 ? .12f : .097f;
                Vector3 Inside(float u, float v) =>
                    Mouth(Mathf.Lerp(.15f,.89f,u),Mathf.Lerp(.061f,.939f,v)) + Vector3.back * depth;
                Patch(root, key, "进气道内壁" + id, ductBack, Inside, 4, 12, flip: flip);
                AddVentRim(root, key, id, Mouth, Inside, ductWall, band != 0);
                float level = wedge ? .49f : .39f;
                Patch(root, key, "进气道导流叶片" + id, shadow, (u, v) =>
                    Vector3.Lerp(Mouth(level + .065f * u, Mathf.Lerp(.05f,.95f,v)),
                        Inside(level + .045f * u, v), .50f),
                    2, 12, flip: flip);
            }
        }

        private static void AddVentRim(Transform root, string key, string id, CarSurface opening, CarSurface inside,
            Material material, bool outer)
        {
            Mesh(root, "进气口倒角与回折", key + ":vent-rim:" + id, material, () =>
            {
                var faces = new Faces();
                // The center inlet is built in two halves. Its seam should stay open.
                for (int edge = outer ? 0 : 1; edge < 4; edge++)
                {
                    Vector2 Along(float t) => edge switch
                    {
                        0 => new Vector2(t,0), 1 => new Vector2(0,t),
                        2 => new Vector2(t,1), _ => new Vector2(1,t)
                    };
                    Vector3 facingPoint = opening(.5f,.5f) + Vector3.back * .043f;
                    for (int n = 0; n < 10; n++)
                    {
                        Vector2 a = Along(n / 10f), b = Along((n + 1) / 10f);
                        Vector3[] quad = { opening(a.x,a.y), opening(b.x,b.y), inside(b.x,b.y), inside(a.x,a.y) };
                        Vector3 normal = Vector3.Cross(quad[1]-quad[0],quad[2]-quad[0]);
                        if (Vector3.Dot(normal,facingPoint-quad[0]) < 0) Array.Reverse(quad);
                        faces.Face(quad);
                    }
                }
                return faces.Build();
            });
        }

        private static void BuildCutHeadlamp(Transform root, string key, PassengerShape shape, int side,
            Material paintedEdge, Material seal, Material lens)
        {
            bool wedge = shape.IsWedge;
            var outline = wedge
                ? new[] { new Vector2(.875f,.746f), new Vector2(.911f,.659f), new Vector2(.958f,.520f),
                    new Vector2(.949f,.626f), new Vector2(.910f,.766f) }
                : new[] { new Vector2(.870f,.738f), new Vector2(.912f,.651f), new Vector2(.963f,.529f),
                    new Vector2(.951f,.619f), new Vector2(.907f,.765f), new Vector2(.885f,.772f) };
            Vector2 center = Vector2.zero;
            foreach (Vector2 point in outline) center += point;
            center /= outline.Length;

            void Plate(string name, Material material, Vector2[] polygon, Vector2 pivot, float scale, float lift)
            {
                Patch(root, key, name + side, material, (u,v) =>
                {
                    float perimeter = v * polygon.Length;
                    int edge = Mathf.Min(Mathf.FloorToInt(perimeter), polygon.Length-1);
                    Vector2 rim = Vector2.Lerp(polygon[edge], polygon[(edge+1)%polygon.Length], perimeter-edge);
                    Vector2 at = Vector2.Lerp(pivot, rim, u*scale);
                    return Offset(shape.BodyPanel,at.x,.5f+side*at.y*.5f,lift);
                }, 6, polygon.Length * 9, flip: side < 0);
            }

            Plate("灯旁漆面折边", paintedEdge, outline, center, 1.14f, .006f);
            Plate("斜切灯座胶边", seal, outline, center, 1f, .011f);
            Plate("收尖灯罩", lens, outline, center, .84f, .015f);
            var light = Mats.Lit(ArtDirection.Hex("D6D8CC"), .42f);
            // The pale reflector repeats the angled edge of the dark lens.
            Vector2 a = Vector2.Lerp(outline[0],center,.29f);
            Vector2 b = Vector2.Lerp(outline[1],center,.26f);
            Vector2 c = Vector2.Lerp(outline[2],center,.34f);
            var stripe = new[] { a,b,c, Vector2.Lerp(c,center,.21f), Vector2.Lerp(b,center,.24f),Vector2.Lerp(a,center,.28f) };
            Plate("灯内折线", light, stripe, (a + b + c) / 3, 1f, .018f);
        }
    }
}
