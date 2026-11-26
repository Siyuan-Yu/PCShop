using System;
using PCShopSell.Core.Session;
using UnityEngine;
using static PCShopSell.Game.Runtime.PropForms;

namespace PCShopSell.Game.Runtime
{
    internal static partial class VehiclePropArt
    {
        private enum Body { Hatch, Fastback, RoundCoupe, TouringCoupe, Wedge, Supercar, BoxVan, PanelVan, Taxi }
        private static Material Rubber => Mats.Lit(ArtDirection.Hex("242624"), .08f);
        private static Material Window => Mats.Lit(ArtDirection.Hex("344145"), .22f);
        private static Material Hub => Mats.Lit(ArtDirection.Hex("A69D89"), .25f);
        private static Material Lamp => Pigment("F2E1B3");
        private static Material Tail => Pigment("A83F31");

        private static Body FormFor(string id) => id switch
        {
            "c_su7" => Body.Fastback, "c_718" => Body.RoundCoupe, "c_921" => Body.TouringCoupe,
            "c_lambo" => Body.Wedge, "c_fa" => Body.Supercar, "v_basic" => Body.BoxVan,
            "v_cargo" => Body.PanelVan, "taxi" => Body.Taxi, _ => Body.Hatch
        };

        internal static Transform Car(Transform parent, string id, Color paint)
        {
            Body form = FormFor(id);
            var root = Group(parent, id ?? "汽车");
            Color finish = form == Body.Wedge
                ? Color.Lerp(paint, ArtDirection.Hex("788052"), .43f)
                : Color.Lerp(paint, ArtDirection.Hex("918771"), .105f);
            bool van = form is Body.BoxVan or Body.PanelVan;
            if (!van && form != Body.Taxi)
                return BuildPassenger(root, form, finish);

            var body = Pigment(finish);
            var shade = Pigment(Color.Lerp(finish, InkColor, .28f));
            float w = van ? .94f : .85f;
            float z = van ? 1.94f : 1.71f;
            float axle = van ? 1.21f : 1.04f;
            float tire = van ? .33f : .29f;
            float belt = van ? .90f : .82f;
            float roof = form == Body.BoxVan ? 1.82f : van ? 1.88f : 1.39f;
            float frontHeight = van ? .87f : .67f;
            float rearHeight = van ? belt - .055f : .68f;
            const float noseDrop = .068f;
            Shell(root, form + ":下车体", body,
                new Section(-z, w * .77f, .24f, rearHeight - .04f),
                new Section(-z + .22f, w * .96f, .23f, rearHeight),
                new Section(-axle, w, .21f, belt - .02f),
                new Section(.20f, w * .97f, .19f, belt),
                new Section(axle, w, .21f, frontHeight + .035f),
                new Section(z - .25f, w * .95f, .24f, frontHeight),
                new Section(z - .09f, w * .86f, .26f, frontHeight - noseDrop * .61f),
                new Section(z, w * .88f, .30f, frontHeight - noseDrop));

            float hood = van ? 1.59f : .94f;
            float glassBase = belt - .015f;
            Cabin(root, form, w, w * (van ? .88f : .73f), glassBase, roof,
                van ? -.20f : -1.28f, van ? -.32f : -.82f, van ? .98f : .51f, hood, body);

            foreach (int side in new[] { -1, 1 })
            {
                Wheel(root, new Vector3(side * (w - .025f), tire+.014f, axle), tire, .16f, side, false);
                Wheel(root, new Vector3(side * (w - .025f), tire+.014f, -axle), tire, .16f, side, false);
                Arch(root, form + ":轮拱:" + side, w + .009f, tire, axle, side, shade);
                Box(root, "门下护边", new Vector3(side * w * .978f, .29f, 0), new Vector3(.034f, .09f, axle * 1.38f), Ink);
                float mirrorY = glassBase + (roof - glassBase) * .098f;
                float mirrorZ = hood - .28f;
                Tube(root, "后视镜杆", new Vector3(side * w * .70f, mirrorY - .039f, mirrorZ-.042f),
                    new Vector3(side * (w + .063f), mirrorY, mirrorZ), .036f, Ink);
                Box(root, "后视镜壳", new Vector3(side * (w + .083f), mirrorY, mirrorZ), new Vector3(.117f, .073f, .104f), body);
                if (!van)
                {
                    Box(root, "门把", new Vector3(side * w * .98f, belt - .075f, .18f), new Vector3(.017f, .027f, .14f), shade);
                    Box(root, "后门把", new Vector3(side * w * .99f, belt - .088f, -.57f), new Vector3(.017f, .027f, .13f), shade);
                }
                float lampX = side * w * .73f;
                Box(root, "方头灯灯壳", new Vector3(lampX, frontHeight - .071f, z-.026f), new Vector3(w*.34f, .117f, .05f), Ink);
                Box(root, "方头灯灯罩", new Vector3(lampX, frontHeight - .065f, z+.004f), new Vector3(w*.28f, .068f, .019f), Lamp);
                Box(root, "尾灯", new Vector3(side * w * .69f, rearHeight - .071f, -z - .004f),
                    new Vector3(w * .24f, .062f, .032f), Tail).localRotation = Quaternion.Euler(-9f, side * -17f, 0);

                if (form is Body.BoxVan or Body.Taxi)
                    for (int chip = 0; chip < 3; chip++)
                        Box(root, "边缘磨漆", new Vector3(side * w * .98f, .307f + .022f * chip, -.24f - .093f * chip),
                            new Vector3(.012f, .014f + .002f * chip, .059f + .008f * chip), Pigment("917559"));
            }
            var air = Box(root, "进气格栅", new Vector3(0, .418f, z - .001f), new Vector3(w * 1.11f, .125f, .042f), Rubber);
            for (int bar = 0; bar < 3; bar++)
                Box(air, "格栅横档", new Vector3(0, -.21f + .22f * bar, .51f), new Vector3(.79f, .04f, .10f), Hub);
            Box(root, "前保险杠", new Vector3(0, .302f, z - .076f), new Vector3(w * 1.76f, .075f, .153f), Rubber);
            Box(root, "车牌", new Vector3(0, .353f, z + .034f), new Vector3(.22f, .069f, .021f), Pigment("516C86"));
            Box(root, "后保险杠", new Vector3(0, .30f, -z + .015f), new Vector3(w * 1.69f, .09f, .129f), Rubber);

            if (van) Cargo(root, form, w, roof, body, shade);
            else TaxiSign(root, roof, w);
            return root;
        }

        private readonly struct Section
        {
            internal readonly float Z, W, Bottom, Top;
            internal Section(float z, float w, float bottom, float top) { Z = z; W = w; Bottom = bottom; Top = top; }
            internal Vector3[] Ring() => new[]
            {
                new Vector3(-W*.78f, Bottom, Z), new Vector3(-W, Bottom + .08f, Z),
                new Vector3(-W*.97f, Mathf.Lerp(Bottom,Top,.76f), Z), new Vector3(-W*.80f, Top, Z),
                new Vector3(W*.80f, Top, Z), new Vector3(W*.97f, Mathf.Lerp(Bottom,Top,.76f), Z),
                new Vector3(W, Bottom + .08f, Z), new Vector3(W*.78f, Bottom, Z)
            };
        }

        private static void Shell(Transform root, string key, Material material, params Section[] stations)
        {
            Mesh(root, "车体折面", "vehicle:" + key, material, () =>
            {
                var faces = new Faces();
                var back = stations[0].Ring();
                faces.Face(back);
                for (int i = 1; i < stations.Length; i++)
                {
                    var front = stations[i].Ring();
                    for (int v = 0; v < back.Length; v++)
                    {
                        int next = (v + 1) % back.Length;
                        faces.Face(back[next], back[v], front[v], front[next]);
                    }
                    back = front;
                }
                Array.Reverse(back);
                faces.Face(back);
                return faces.Build();
            });
        }

        private static void Cabin(Transform root, Body form, float width, float topWidth, float y, float roof,
            float back, float topBack, float topFront, float front, Material paint)
        {
            float sill = width * .88f;
            Vector3 Low(int side, bool forward) => new Vector3(side * sill, y, forward ? front : back);
            Vector3 High(int side, bool forward) => new Vector3(side * topWidth, roof, forward ? topFront : topBack);
            Mesh(root, "顶棚和窗柱", "vehicle:roof:" + form, paint, () =>
            {
                var faces = new Faces();
                faces.Face(High(-1,false), High(-1,true), High(1,true), High(1,false));
                faces.Face(Low(-1, false), High(-1, false), High(1, false), Low(1, false));
                faces.Face(Low(1, true), High(1, true), High(-1, true), Low(-1, true));
                faces.Face(Low(-1,true), High(-1,true), High(-1,false), Low(-1,false));
                faces.Face(Low(1,false), High(1,false), High(1,true), Low(1,true));
                return faces.Build();
            });
            Mesh(root, "挡风玻璃及侧窗", "vehicle:windows:" + form, Window, () =>
            {
                var faces = new Faces();
                foreach (int end in new[] { -1, 1 })
                {
                    bool forward = end > 0;
                    Vector3 GlassPoint(int side, float height)
                    {
                        var p = Vector3.Lerp(Low(side, forward), High(side, forward), height);
                        p.x *= .89f;
                        return p + Vector3.forward * end * .015f + Vector3.up * .008f;
                    }
                    if (forward) faces.Face(GlassPoint(1,.11f), GlassPoint(1,.88f), GlassPoint(-1,.88f), GlassPoint(-1,.11f));
                    else faces.Face(GlassPoint(-1,.12f), GlassPoint(-1,.86f), GlassPoint(1,.86f), GlassPoint(1,.12f));
                }
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 Pane(float length, float high)
                    {
                        var a = Vector3.Lerp(Low(side,false), Low(side,true), length);
                        var b = Vector3.Lerp(High(side,false), High(side,true), length);
                        return Vector3.Lerp(a,b,high) + Vector3.right * side * .017f;
                    }
                    void WindowSegment(float low, float high)
                    {
                        var points = new[]{Pane(low,.12f),Pane(low,.88f),Pane(high,.88f),Pane(high,.12f)};
                        if (side < 0) Array.Reverse(points);
                        faces.Face(points);
                    }
                    WindowSegment(.07f, .48f);
                    WindowSegment(.54f, .92f);
                }
                return faces.Build();
            });
            Box(root, "顶板折边", new Vector3(0, roof + .019f, (topFront + topBack) * .5f),
                new Vector3(topWidth * 1.96f, .042f, topFront - topBack), paint);
        }

        private static void Arch(Transform parent, string key, float w, float tire, float axle, int side, Material mat)
        {
            Mesh(parent, "轮拱边", "vehicle:arch:" + key, mat, () =>
            {
                var arcs = new Faces();
                Vector3 At(float angle, float radius, float center) =>
                    new Vector3(w * side, tire+.014f + Mathf.Sin(angle) * radius, center + Mathf.Cos(angle) * radius);
                for (int wheel = -1; wheel <= 1; wheel += 2)
                for (int n = 0; n < 8; n++)
                {
                    float first = Mathf.Lerp(-.06f, Mathf.PI+.06f, n/8f);
                    float second = Mathf.Lerp(-.06f, Mathf.PI+.06f, (n+1)/8f);
                    var quad = new[] { At(first,tire+.012f,wheel*axle), At(second,tire+.012f,wheel*axle),
                        At(second,tire+.047f,wheel*axle), At(first,tire+.047f,wheel*axle) };
                    if (side < 0) Array.Reverse(quad);
                    arcs.Face(quad);
                }
                return arcs.Build();
            });
        }

        internal static void Wheel(Transform root, Vector3 center, float radius, float width, int side, bool sport, bool roundedTire = false)
        {
            float face = center.x + side * width * .50f;
            if (roundedTire) RoundedTire(root, center, radius, width);
            else
            {
                Tube(root, "橡胶胎", center - Vector3.right * width * .50f, center + Vector3.right * width * .50f, radius * 2, Rubber);
                Tube(root, "胎肩", center - Vector3.right * width * .53f, center + Vector3.right * width * .53f, radius * 1.84f, Ink);
            }
            Vector3 disk = new Vector3(face, center.y, center.z);
            Material alloy = roundedTire ? Mats.Lit(ArtDirection.Hex("8B8D87"), .31f) : Hub;
            Tube(root, "轮毂外圈", disk, disk + Vector3.right * side * .020f, radius * 1.27f, alloy);
            if (sport || roundedTire)
            {
                Tube(root, "刹车盘", disk + Vector3.right * side * .022f, disk + Vector3.right * side * .027f, radius * 1.07f, Rubber);
                int spokes = sport ? 5 : 6;
                for (int i = 0; i < spokes; i++)
                {
                    float a = i * Mathf.PI * 2 / spokes;
                    Vector3 hub = disk + Vector3.right * side * .033f;
                    Tube(root, "轮毂辐条", hub, hub + new Vector3(0,Mathf.Sin(a),Mathf.Cos(a))*radius*.55f, .025f, alloy);
                }
            }
            Tube(root, "轴盖", disk + Vector3.right*side*.024f, disk + Vector3.right * side * .045f, radius * .42f, sport ? alloy : Ink);
        }

        internal static void Scooter(Transform root, MotorModel model, Color color)
        {
            bool three = model.Kind == VehicleKind.Tricycle;
            var paint = Pigment(color);
            var inset = Pigment(Color.Lerp(color, InkColor, .22f));
            var steel = Pigment("877E70");
            float seatZ = three ? -.24f : -.38f;
            float cushion = three ? .215f : .285f, length = three ? .205f : .345f;
            Shell(root, three ? "三轮鞍座" : "踏板车鞍座", Rubber,
                new Section(seatZ-length,cushion*.66f,.756f,.841f), new Section(seatZ-length+.11f,cushion,.753f,.888f),
                new Section(seatZ+length*.42f,cushion*.91f,.75f,.88f), new Section(seatZ+length,cushion*.63f,.76f,.818f));
            Wheel(root, new Vector3(0,.30f,.70f), .285f,.12f, 1, false);
            if (three)
            {
                for (int side = -1; side <= 1; side += 2)
                    Wheel(root, new Vector3(side*.64f,.30f,-.72f), .285f,.15f,side,false);
                Box(root, "后桥", new Vector3(0,.36f,-.72f), new Vector3(1.28f,.10f,.13f), steel);
                Box(root, "货斗底", new Vector3(0,.57f,-.79f), new Vector3(1.30f,.105f,1.09f), paint);
                Box(root, "货斗内衬", new Vector3(0,.629f,-.79f), new Vector3(1.19f,.026f,.97f), inset);
                for (int side = -1; side <= 1; side += 2)
                {
                    Box(root, "货斗侧板", new Vector3(side*.623f,.774f,-.79f), new Vector3(.057f,.35f,1.09f), paint);
                    Box(root, "货斗侧板角钢", new Vector3(side*.635f,.945f,-.79f), new Vector3(.073f,.039f,1.10f), inset);
                    Box(root, "货斗卷边", new Vector3(0,.77f,-.79f + side*.522f), new Vector3(1.27f,.34f,.054f), paint);
                    Box(root, "尾门搭扣", new Vector3(side*.53f,.738f,-1.35f), new Vector3(.038f,.16f,.037f), steel);
                    Box(root, "三轮尾灯", new Vector3(side*.56f,.51f,-1.36f), new Vector3(.095f,.053f,.028f), Tail);
                }
                Tube(root, "贯通车架", new Vector3(0,.33f,-.58f), new Vector3(0,.53f,.56f), .15f, paint);
                Tube(root, "鞍座柱", new Vector3(0,.37f,-.17f), new Vector3(0,.77f,-.24f), .081f, steel);
                Tube(root, "斜撑", new Vector3(0,.67f,-.23f), new Vector3(0,.91f,.55f), .075f, paint);
            }
            else
            {
                Wheel(root, new Vector3(0,.30f,-.70f), .285f,.13f, -1, false);
                Box(root, "踏板车底架", new Vector3(0,.35f,.005f), new Vector3(.47f,.13f,1.58f), paint);
                Box(root, "防滑踏板", new Vector3(0,MotorVisual.ScooterDeckHeight-.025f,.09f),
                    new Vector3(.53f,.05f,.66f), Rubber);
                for (int groove = 0; groove < 4; groove++)
                    Box(root, "踏板防滑筋", new Vector3((groove-1.5f)*.103f,MotorVisual.ScooterDeckHeight+.002f,.09f),
                        new Vector3(.016f,.007f,.55f), inset);
                Shell(root, "踏板后罩", paint,
                    new Section(-1.007f,.12f,.405f,.56f), new Section(-.938f,.22f,.373f,.647f), new Section(-.81f,.288f,.34f,.725f),
                    new Section(-.51f,.30f,.36f,.755f), new Section(-.27f,.25f,.392f,.732f), new Section(-.13f,.146f,.415f,.626f));
                Shell(root, "踏板前围", paint,
                    new Section(.395f,.188f,.41f,.99f), new Section(.448f,.244f,.397f,1.046f), new Section(.546f,.269f,.425f,1.063f),
                    new Section(.622f,.233f,.482f,1.026f), new Section(.681f,.134f,.584f,.957f));
                Box(root, "尾灯底座", new Vector3(0,.55f,-.973f), new Vector3(.285f,.112f,.042f), Ink);
                Box(root, "尾部红灯", new Vector3(0,.555f,-1.001f), new Vector3(.235f,.073f,.024f), Tail);
                if (model.Id == "e_range") Box(root, "后置储物箱", new Vector3(0,.955f,-.86f),
                    new Vector3(.52f,.28f,.35f), paint);
                if (model.Id is "e_dash" or "e_race")
                {
                    Box(root, "低风挡", new Vector3(0,1.35f,.51f), new Vector3(.40f,.33f,.042f), Window)
                        .localRotation = Quaternion.Euler(-18,0,0);
                    Box(root, "风挡包边", new Vector3(0,1.487f,.472f), new Vector3(.385f,.024f,.05f), steel);
                }
                if (model.Id == "e_turn") Box(root, "前围饰条", new Vector3(0,.82f,.66f), new Vector3(.17f,.30f,.021f), Cream);
            }

            float handleZ = three ? .61f : MotorVisual.ScooterGrip(0).z;
            float handleY = three ? 1.14f : MotorVisual.ScooterGrip(0).y;
            float handleX = three ? .43f : MotorVisual.ScooterGrip(1).x;
            for (int side = -1; side <= 1; side += 2)
            {
                Tube(root, "前叉", new Vector3(side*.085f,.32f,.70f), new Vector3(side*.071f,handleY-.15f,handleZ+.045f), .040f, steel);
                Tube(root, "把手转角", new Vector3(0,handleY-.023f,handleZ+.11f), new Vector3(side*handleX,handleY,handleZ), .044f, steel);
                Box(root, "橡胶握把", new Vector3(side*handleX,handleY,handleZ), new Vector3(.145f,.063f,.065f), Rubber);
            }
            Shell(root, "前挡泥板", paint, new Section(.43f,.095f,.48f,.52f), new Section(.64f,.12f,.56f,.615f),
                new Section(.82f,.112f,.51f,.585f), new Section(.97f,.077f,.43f,.492f));
            var headlight = new Vector3(0,handleY-.13f,three?.75f:.70f);
            Tube(root, "圆灯框", headlight - Vector3.forward*.034f, headlight + Vector3.forward*.036f, .238f, inset);
            Tube(root, "圆灯透镜", headlight + Vector3.forward*.036f, headlight + Vector3.forward*.046f, .173f, Lamp);
            Tube(root, "左镜杆", new Vector3(-handleX*.84f,handleY,handleZ),
                new Vector3(-handleX*1.13f,handleY+.19f,handleZ+.01f),.024f,steel);
            Tube(root, "反光镜", new Vector3(-handleX*1.13f,handleY+.19f,handleZ-.008f),
                new Vector3(-handleX*1.13f,handleY+.19f,handleZ+.035f),.104f,Hub);
            if (model.Id is "e_city" or "t_cargo")
                foreach (int side in new[] { -1, 1 })
                    for (int i = 0; i < 4; i++)
                        Box(root, "磨漆小斑", new Vector3(side*(three?.654f:.287f), (three?.674f:.456f)+i*.018f, (three?-.96f:-.69f)-i*.042f),
                            new Vector3(.011f,.013f+i*.003f,.037f), Pigment("A48161"));
        }

        private static void Cargo(Transform root, Body form, float w, float roof, Material paint, Material seam)
        {
            float length = form == Body.BoxVan ? 2.09f : 2.24f;
            float top = form == Body.BoxVan ? roof + .115f : roof;
            float center = -.76f;
            Box(root, "货厢", new Vector3(0, (top + .56f) * .5f, center), new Vector3(w*1.93f, top-.56f, length), paint);
            Box(root, "货厢腰轨", new Vector3(0, .67f, center), new Vector3(w*1.96f, .058f, length), seam);
            float aft = center - length/2f;
            Box(root, "对开门缝", new Vector3(0, (top+.72f)*.5f, aft-.018f), new Vector3(.018f, top-.78f, .028f), seam);
            for (int side = -1; side <= 1; side += 2)
            {
                Box(root, "货厢外角压条", new Vector3(side*w*.91f, (top+.68f)*.5f, aft-.023f), new Vector3(.035f, top-.73f, .037f), seam);
                Box(root, "货门锁扣", new Vector3(side*.12f, 1.02f, aft-.037f), new Vector3(.12f, .034f, .028f), Ink);
                if (form == Body.PanelVan) Box(root, "滑门分缝", new Vector3(side*w*.969f, 1.24f, -.22f),
                    new Vector3(.01f, .94f, .023f), seam);
                if (form == Body.BoxVan)
                {
                    for (int hinge = 0; hinge < 2; hinge++)
                        Box(root, "货门铰链", new Vector3(side*w*.76f, .91f+hinge*.69f, aft-.029f), new Vector3(.12f,.048f,.035f), Hub);
                    Box(root, "旧厢底部的局部补漆", new Vector3(side*w*.972f, .73f, -1.40f),
                        new Vector3(.010f, .08f, .29f), Pigment("B5AC98"));
                }
            }
        }

        private static void TaxiSign(Transform root, float roof, float w)
        {
            Box(root, "出租车顶灯", new Vector3(0, roof+.123f, -.10f), new Vector3(.60f,.16f,.24f), Cream);
            var plate = Group(root,"车顶灯正面",new Vector3(0,roof+.128f,.026f));
            plate.localRotation = Quaternion.Euler(0,180,0);
            Prims.SignText(plate,"出租车",Vector3.zero,.53f,.107f,InkColor);
            for (int side=-1; side<=1; side+=2)
            {
                var door = Group(root,"出租车门字",new Vector3(side*w*.996f,.59f,.13f));
                door.localRotation = Quaternion.Euler(0,-90*side,0);
                Prims.SignText(door,"出租车",Vector3.zero,.44f,.11f,InkColor);
            }
        }
    }
}
