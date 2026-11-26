using System.Collections.Generic;
using PCShopSell.Core.Home;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    /// <summary>挂在摆出来的家具上：知道自己是第几件、是什么。点击摆放模式下用来拾起，平时用来触发互动。</summary>
    public sealed class PlacedVisual : MonoBehaviour
    {
        public int Index;
        public string FurnitureId;
    }

    /// <summary>
    /// 家具模型。局部坐标：原点在占地中心的地面上，宽沿 X，进深沿 Z，正面朝 -Z；
    /// 墙面家具的原点在墙面上、朝屋内是 -Z。摆放时按 Rot 转 90° 的倍数。
    /// </summary>
    public static class FurnitureVisuals
    {
        private static readonly Color Metal = new Color(0.22f, 0.22f, 0.25f);
        internal const float StoolSeatHeight = .4f;
        internal const float SofaSeatHeight = .45f;
        internal const float DiningSeatHeight = .41f;
        internal static readonly Vector3 DiningSeatPosition = new Vector3(0, 0, -.28f);
        private static readonly Vector3 DiningPlatePosition = new Vector3(0, .778f, .115f);
        internal static readonly Vector3 DiningMealPosition = DiningPlatePosition + Vector3.up * .038f;

        public static GameObject Build(Transform parent, FurnitureDef def, bool collider, string shelfKind = null, int district = 0)
        {
            var root = new GameObject("F_" + def.Id).transform;
            root.SetParent(parent, false);
            float w = (float)def.Width, d = (float)def.Depth;
            bool c = collider;
            if (def.IsFixture)
            {
                ShopFixtures.BuildFixture(root, def, c, shelfKind, district);
                return root.gameObject;
            }
            if(def.Id.StartsWith("restaurant_")||def.Id.StartsWith("used_")||def.Id==FurnitureCatalog.ModelPaintBench){SpecialFurniture(root,def,c);return root.gameObject;}
            if(PetShopCatalog.HouseCapacity(def.Id)>0 || PetShopCatalog.IsToyFixture(def.Id))
            {
                PetSceneModels.BuildFurniture(root,def,c);
                return root.gameObject;
            }
            switch (def.Id)
            {
                case FurnitureCatalog.FittingMirror:
                    Prims.Box(root,"MirrorFrame",new Vector3(0,.92f,0),new Vector3(.7f,1.8f,.09f),Mats.Lit(ArtDirection.Wood),c);
                    Prims.Box(root,"MirrorGlass",new Vector3(0,.96f,-.05f),new Vector3(.60f,1.56f,.012f),Mats.Lit(new Color(.66f,.81f,.86f),.36f));
                    Prims.Box(root,"MirrorBevel",new Vector3(-.285f,.96f,-.058f),new Vector3(.018f,1.48f,.005f),PropForms.Pigment("AFCED1"));
                    Prims.Box(root,"MirrorBevel",new Vector3(0,1.724f,-.058f),new Vector3(.56f,.017f,.005f),PropForms.Pigment("AFCED1"));
                    Prims.Box(root,"MirrorFoot",new Vector3(0,.06f,0),new Vector3(.70f,.12f,.25f),Mats.Lit(Metal),c);
                    break;
                case FurnitureCatalog.BedBoard: Bed(root, w, d, c, 0, district); break;
                case "bed_spring": Bed(root, w, d, c, 1, district); break;
                case "bed_good": Bed(root, w, d, c, 2, district); break;
                case FurnitureCatalog.WardrobeCloth: FurniturePropArt.Wardrobe(root, w, d, false, c, district); break;
                case "wardrobe_wood": FurniturePropArt.Wardrobe(root, w, d, true, c, district); break;
                case FurnitureCatalog.FridgeSmall:
                case FurnitureCatalog.Fridge: FurniturePropArt.Refrigerator(root, def, c); break;
                case FurnitureCatalog.TableCooker: TableCooker(root, w, d, c); break;
                case "stool": Stool(root, c); break;
                case "fan_broken": Fan(root, c, true); break;
                case "fan_new":
                case "fan_fixed": Fan(root, c, false); break;
                case "junk": Junk(root, c); break;
                case FurnitureCatalog.HomeBox:
                case FurnitureCatalog.ShopBox: FurniturePropArt.Carton(root,"Carton",Vector3.zero,new Vector3(w-.04f,.5f,d-.04f),c); break;
                case "aircon_large":
                case "aircon":
                    FurniturePropArt.AirConditioner(root,w);
                    if(c){var click=root.gameObject.AddComponent<BoxCollider>();click.center=new Vector3(0,2.4f,-.15f);click.size=new Vector3(w,.5f,.35f);}
                    break;
                case FurnitureCatalog.Curtain: FurniturePropArt.Drapes(root); break;
                case "poster": Poster(root, w, 1.0f, 1.55f, new Color(0.85f, 0.55f, 0.25f), new Color(0.25f, 0.45f, 0.65f)); break;
                case "rug": Rug(root, w, d); break;
                case "plant": FurniturePropArt.Plant(root, .4f, .9f, c, false); break;
                case "shop_plant": FurniturePropArt.Plant(root, .5f, 1.3f, c, true); break;
                case FurnitureCatalog.HomeWorkbench: HomeWorkbench(root, w, d, c); break;
                case "desk": Desk(root, w, d, c, district: district); break;
                case "sofa": FurniturePropArt.Sofa(root, w, d, c, ArtDirection.Hex("946E52"), district); break;
                case "shop_sofa": FurniturePropArt.Sofa(root, w, d, c, ArtDirection.Hex("546C84"), district); break;
                case "shop_dispenser": FurniturePropArt.Dispenser(root, c); break;
                case "shop_poster": Poster(root, w, 1.3f, 2.55f, new Color(0.9f, 0.3f, 0.25f), new Color(0.95f, 0.9f, 0.4f)); break;
                case "shop_led": Led(root, w); break;
                default:
                    Prims.Box(root, "Body", new Vector3(0, 0.25f, 0), new Vector3(w, 0.5f, d), Mats.Lit(new Color(0.6f, 0.55f, 0.5f)), c);
                    break;
            }
            return root.gameObject;
        }

        private static void SpecialFurniture(Transform root, FurnitureDef def, bool collider)
        {
            float w = (float)def.Width, d = (float)def.Depth;
            if (def.Id == "restaurant_table")
            {
                RestaurantDiningSet(root, w, d, collider);
                return;
            }

            Material steel = PropForms.Pigment("838882");
            Material dark = PropForms.Pigment("423D37");
            Material wood = PropForms.Wood("96704D");
            if (def.Id == "restaurant_stove")
            {
                PropForms.Box(root, "StoveBody", new Vector3(0, .42f, 0), new Vector3(w, .84f, d), steel, collider);
                PropForms.Box(root, "StoveTop", 0, .854f, 0, w, .045f, d, dark);
                PropForms.Box(root, "ServicePanel", 0, .393f, -.5f * d - .008f, w * .82f, .48f, .018f, PropForms.Pigment("68706D"));
                for (int i = 0; i < 3; i++)
                    PropForms.Tube(root, "ControlKnob", new Vector3((i - 1) * .142f, .735f, -.5f * d),
                        new Vector3((i - 1) * .142f, .735f, -.5f * d - .034f), .061f, i == 1 ? PropForms.Pigment("A65943") : dark);
                Prims.Shape(PrimitiveType.Cylinder, root, "Burner", new Vector3(0, .902f, 0),
                    new Vector3(.31f, .032f, .31f), dark);
                PropForms.Revolve(root, "Wok", PropForms.Pigment("4F4D45"), "kitchen-wok", new Vector3(0, .905f, 0),
                    new Vector3(.49f, .157f, .49f), new Vector2(0, 0), new Vector2(.207f, 0),
                    new Vector2(.339f, .35f), new Vector2(.46f, .75f), new Vector2(.50f, .94f),
                    new Vector2(.48f, 1), new Vector2(.42f, .81f), new Vector2(.29f, .38f),
                    new Vector2(.145f, .25f), new Vector2(0, .25f));
                PropForms.Tube(root, "WokHandle", new Vector3(.215f, 1.019f, 0), new Vector3(.379f, 1.065f, 0), .046f, wood);
                Prims.Shape(PrimitiveType.Sphere, root, "FoodInWok", new Vector3(0, .979f, 0), new Vector3(.275f, .036f, .275f), PropForms.Pigment("B88B52"));
                return;
            }
            if (def.Id == "used_display")
            {
                PropForms.Box(root, "DisplayBase", new Vector3(0, .419f, 0), new Vector3(w, .838f, d), wood, collider);
                PropForms.Box(root, "FeltBed", 0, .849f, 0, w - .06f, .026f, d - .06f, PropForms.Pigment("627369"));
                Material glass = DistrictFinishes.VillaPane(balustrade: true);
                PropForms.Box(root, "GlassTop", 0, 1.128f, 0, w - .04f, .012f, d - .04f, glass);
                foreach (int side in new[] { -1, 1 })
                {
                    PropForms.Box(root, "GlassSide", side * (w / 2 - .022f), .988f, 0, .01f, .272f, d - .046f, glass);
                    PropForms.Box(root, "GlassFrontBack", 0, .988f, side * (d / 2 - .022f), w - .046f, .272f, .01f, glass);
                    foreach (int end in new[] { -1, 1 })
                        PropForms.Box(root, "GlassPost", side * (w / 2 - .02f), .982f, end * (d / 2 - .02f), .028f, .285f, .028f, dark);
                }
                PropForms.Box(root, "DrawerFront", 0, .669f, -d / 2 - .006f, w - .12f, .188f, .022f, PropForms.Wood("AE845A"));
                PropForms.Box(root, "DrawerPull", 0, .686f, -d / 2 - .026f, .14f, .022f, .028f, dark);
                return;
            }

            Desk(root, w, d, collider, withLaptop: false);
            switch (def.Id)
            {
                case FurnitureCatalog.LegacyRestaurantPrep:
                    PropForms.Box(root, "Cabinet", 0, .424f, .023f, w - .16f, .52f, d - .1f, wood);
                    PropForms.Box(root, "SteelWorktop", 0, .786f, 0, w, .043f, d, steel);
                    PropForms.Box(root, "ChoppingBoard", .072f, .822f, -.03f, .49f, .033f, .317f, PropForms.Wood("BC925D"));
                    PropForms.Box(root, "KnifeBlade", .123f, .847f, .036f, .19f, .008f, .054f, steel);
                    PropForms.Box(root, "KnifeHandle", .263f, .848f, .034f, .105f, .022f, .038f, dark);
                    break;
                case FurnitureCatalog.LegacyRestaurantPass:
                    PropForms.Box(root, "PassCounter", 0, .788f, 0, w, .044f, d, steel);
                    PropForms.Box(root, "PassTop", 0, 1.302f, .10f, w, .051f, .29f, steel);
                    foreach (int side in new[] { -1, 1 })
                        PropForms.Box(root, "ShelfRiser", side * (w / 2 - .042f), 1.049f, .178f, .045f, .49f, .044f, steel);
                    PropForms.Box(root, "WarmingLamp", 0, 1.268f, .061f, w - .18f, .021f, .047f,
                        Mats.Glow(ArtDirection.Hex("D49A52"), new Color(.65f,.27f,.08f), false));
                    break;
                case "used_buydesk":
                    PropForms.Box(root, "InspectionMat", -.12f, .776f, -.05f, .54f, .017f, .37f, PropForms.Pigment("4F685B"));
                    PropForms.Box(root, "ScaleBase", .327f, .828f, -.05f, .265f, .114f, .219f, steel);
                    PropForms.Box(root, "ScaleTray", .327f, .897f, -.04f, .243f, .022f, .167f, PropForms.Pigment("AAA797"));
                    PropForms.Box(root, "ScaleReadout", .327f, .845f, -.163f, .115f, .034f, .013f, dark);
                    Vector3 low = new Vector3(-.38f, .789f, .206f), hinge = new Vector3(-.38f, 1.275f, .18f);
                    PropForms.Tube(root, "LampArm", low, hinge, .027f, dark);
                    PropForms.Tube(root, "LampArm", hinge, new Vector3(-.30f, 1.331f, -.038f), .027f, dark);
                    PropForms.Box(root, "LampShade", -.30f, 1.315f, -.065f, .205f, .080f, .117f, PropForms.Pigment("777D5C"));
                    PropForms.Box(root, "Diffuser", -.30f, 1.273f, -.065f, .174f, .012f, .091f, PropForms.Cream);
                    break;
                case "used_repairbench":
                    PropForms.Box(root, "RepairMat", 0, .791f, -.031f, w * .72f, .02f, d * .59f, PropForms.Pigment("637666"));
                    PropForms.Box(root, "ToolRack", 0, 1.081f, d * .425f, w * .84f, .447f, .076f, wood);
                    for (int i = 0; i < 6; i++)
                        PropForms.Box(root, "RackSlot", (i - 2.5f) * w * .119f, 1.165f, d * .425f - .042f, .039f, .027f, .013f, dark);
                    for (int i = 0; i < 4; i++)
                    {
                        float x = (i - 1.5f) * w * .14f;
                        PropForms.Box(root, "ToolShank", x, 1.106f, d * .425f - .054f, .025f, .162f, .021f, steel);
                        PropForms.Box(root, "ToolGrip", x, 1.042f, d * .425f - .058f, .043f, .083f, .033f, PropForms.Pigment(i % 2 == 0 ? "976044" : "5B625B"));
                    }
                    PropForms.Box(root, "Vise", w * .289f, .866f, -.11f, .211f, .171f, .15f, PropForms.Pigment("586A6C"));
                    PropForms.Box(root, "ViseJaw", w * .289f, .958f, -.164f, .206f, .033f, .058f, steel);
                    PropForms.Tube(root, "ViseHandle", new Vector3(w * .289f - .082f, .873f, -.202f),
                        new Vector3(w * .289f + .082f, .873f, -.202f), .015f, steel);
                    break;
                default:
                    Material enamel = PropForms.Pigment("7F887A");
                    PropForms.Box(root, "SprayBooth", 0, 1.087f, d * .385f, w * .78f, .558f, .08f, enamel);
                    PropForms.Box(root, "SprayHood", 0, 1.389f, .161f, w * .78f, .077f, .43f, enamel);
                    foreach (int side in new[] { -1, 1 })
                        PropForms.Box(root, "SpraySide", side * w * .365f, 1.108f, .163f, .043f, .508f, .361f, enamel);
                    PropForms.Box(root, "FilterFrame", 0, 1.144f, d * .385f - .047f, .53f, .256f, .019f, dark);
                    for (int i = 0; i < 8; i++)
                        PropForms.Box(root, "MeshFilter", 0, 1.023f + i * .031f, d * .385f - .062f, .499f, .014f, .009f, steel);
                    Prims.Shape(PrimitiveType.Cylinder, root, "Turntable", new Vector3(0, .808f, -.017f), new Vector3(.281f, .037f, .281f), dark);
                    PropForms.Tube(root, "Airbrush", new Vector3(.235f,.797f,-.196f), new Vector3(.378f,.802f,-.156f), .024f, steel);
                    for (int i = 0; i < 3; i++)
                        Prims.Shape(PrimitiveType.Cylinder, root, "PaintCup", new Vector3(-.32f, .811f, -.138f + i * .084f),
                            new Vector3(.057f, .045f, .057f), PropForms.Pigment(i == 0 ? "AD6C51" : i == 1 ? "BCA067" : "6A7D6C"));
                    break;
            }
        }

        private static void RestaurantDiningSet(Transform root, float width, float depth, bool collider)
        {
            // Keep the table, chair and the gap inside the original placement footprint.
            const float tableDepth = .46f;
            var table = new GameObject("DiningTable").transform;
            table.SetParent(root, false);
            table.localPosition = new Vector3(0, 0, (depth - tableDepth) * .5f);
            Desk(table, width, tableDepth, collider, withLaptop: false);
            Prims.Shape(PrimitiveType.Cylinder, root, "DiningPlate", DiningPlatePosition,
                new Vector3(.26f, .013f, .26f), Mats.Lit(Color.white));

            var chair = new GameObject("DiningChair").transform;
            chair.SetParent(root, false);
            chair.localPosition = DiningSeatPosition;
            var wood = Mats.Lit(ArtDirection.Wood);
            var frame = Mats.Lit(Metal);
            Prims.Box(chair, "Seat", new Vector3(0, DiningSeatHeight - .025f, 0), new Vector3(.36f, .05f, .29f), wood, collider);
            Prims.Box(chair, "Backrest", new Vector3(0, .665f, -.12f), new Vector3(.36f, .16f, .05f), wood, collider);
            foreach (float x in new[] { -.15f, .15f })
            {
                Prims.Box(chair, "FrontLeg", new Vector3(x, .19f, .11f), new Vector3(.045f, .38f, .045f), frame);
                Prims.Box(chair, "BackPost", new Vector3(x, .35f, -.12f), new Vector3(.045f, .7f, .045f), frame);
            }
        }

        /// <summary>把模型摆到房间里的位置上。</summary>
        public static void Place(GameObject go, FurnitureDef def, PlacedFurniture p, RoomSpec room)
        {
            Transform t = go.transform;
            if (def.Slot == FurnitureSlot.Wall)
            {
                if (p.Rot == 1)
                {
                    t.localPosition = new Vector3((float)room.Width, 0, (float)p.Z);
                    t.localRotation = Quaternion.Euler(0, 90, 0);
                }
                else
                {
                    t.localPosition = new Vector3((float)p.X, 0, (float)room.Depth);
                    t.localRotation = Quaternion.identity;
                }
                return;
            }
            t.localPosition = new Vector3((float)p.X, 0, (float)p.Z);
            t.localRotation = Quaternion.Euler(0, 90f * p.Rot, 0);
        }

        // ---------- 床 ----------

        private static void Bed(Transform r, float w, float d, bool c, int grade, int district)
        {
            float hx = -w / 2;
            if (district == 3 && grade == 2)
            {
                var ivory = PropForms.Pigment("D0D3CC");
                PropForms.Box(r, "BedRecessedSupport", new Vector3(0,.188f,0),new Vector3(w*.78f,.36f,d*.76f),PropForms.Ink,c);
                PropForms.Box(r, "BedPlatform", 0,.375f,0,w,.106f,d,PropForms.Wood("846A55"));
                for (int side = -1; side <= 1; side += 2)
                    PropForms.Box(r,"ToppedMattress",0,.528f,side*d*.237f,w-.13f,.207f,d*.462f,ivory);
                var underlight = Mats.Glow(ArtDirection.Hex("A1C7C6"),new Color(.23f,.70f,.82f),false);
                for (int side = -1; side <= 1; side += 2)
                {
                    PropForms.Box(r,"BedSideLight",0,.32f,side*(d*.5f-.079f),w-.16f,.019f,.018f,underlight);
                    PropForms.Box(r,"BedEndLight",side*(w*.5f-.079f),.32f,0,.018f,.019f,d-.14f,underlight);
                }
                return;
            }
            if (grade == 0)
            {
                foreach (Vector2 p in new[] { new Vector2(-w / 2 + 0.1f, -d / 2 + 0.1f), new Vector2(w / 2 - 0.1f, -d / 2 + 0.1f), new Vector2(-w / 2 + 0.1f, d / 2 - 0.1f), new Vector2(w / 2 - 0.1f, d / 2 - 0.1f) })
                    Prims.Box(r, "Leg", new Vector3(p.x, 0.15f, p.y), new Vector3(0.06f, 0.3f, 0.06f), Mats.Lit(Metal, 0.3f, 0.5f));
                Prims.Box(r, "Board", new Vector3(0, 0.33f, 0), new Vector3(w, 0.06f, d), FurniturePropArt.TexturedWood(district), c);
                Prims.Box(r, "Head", new Vector3(hx + 0.04f, 0.65f, 0), new Vector3(0.08f, 0.6f, d), Mats.Lit(Metal, 0.3f, 0.5f));
                Prims.Box(r, "Mattress", new Vector3(0.1f, 0.40f, 0), new Vector3(w - 0.2f, 0.08f, d - 0.08f), PropForms.Pigment("ACA07E"));
                Prims.Box(r, "Pillow", new Vector3(hx + 0.4f, 0.47f, 0), new Vector3(0.38f, 0.1f, 0.6f), Mats.Lit(new Color(0.72f, 0.7f, 0.64f), 0.02f));
                Prims.Box(r, "Blanket", new Vector3(0.5f, 0.46f, 0), new Vector3(0.95f, 0.06f, d - 0.1f), Mats.Lit(new Color(0.55f, 0.38f, 0.3f), 0.02f));
            }
            else if (grade == 1)
            {
                Prims.Box(r, "Frame", new Vector3(0, 0.22f, 0), new Vector3(w, 0.3f, d), FurniturePropArt.TexturedWood(district), c);
                Prims.Box(r, "Head", new Vector3(hx + 0.04f, 0.6f, 0), new Vector3(0.08f, 0.7f, d), FurniturePropArt.TexturedWood(district));
                Prims.Box(r, "Mattress", new Vector3(0.05f, 0.47f, 0), new Vector3(w - 0.12f, 0.22f, d - 0.1f), Mats.Lit(new Color(0.45f, 0.62f, 0.74f), 0.05f));
                Prims.Box(r, "Pillow", new Vector3(hx + 0.4f, 0.64f, 0), new Vector3(0.38f, 0.12f, 0.6f), Mats.Lit(new Color(0.9f, 0.9f, 0.88f), 0.05f));
                Prims.Box(r, "Blanket", new Vector3(0.5f, 0.6f, 0), new Vector3(0.95f, 0.07f, d - 0.12f), Mats.Lit(new Color(0.4f, 0.5f, 0.65f), 0.05f));
            }
            else
            {
                Prims.Box(r, "Frame", new Vector3(0, 0.2f, 0), new Vector3(w, 0.34f, d), FurniturePropArt.TexturedWood(district), c);
                Prims.Box(r, "Head", new Vector3(hx + 0.05f, 0.7f, 0), new Vector3(0.1f, 1.0f, d), FurniturePropArt.TexturedWood(district));
                Prims.Box(r, "Mattress", new Vector3(0.05f, 0.5f, 0), new Vector3(w - 0.14f, 0.28f, d - 0.12f), Mats.Lit(new Color(0.93f, 0.9f, 0.82f), 0.1f));
                Prims.Box(r, "PillowA", new Vector3(hx + 0.4f, 0.71f, -d * 0.2f), new Vector3(0.38f, 0.14f, 0.45f), Mats.Lit(new Color(0.95f, 0.95f, 0.95f), 0.1f));
                Prims.Box(r, "PillowB", new Vector3(hx + 0.4f, 0.71f, d * 0.2f), new Vector3(0.38f, 0.14f, 0.45f), Mats.Lit(new Color(0.95f, 0.95f, 0.95f), 0.1f));
                Prims.Box(r, "Blanket", new Vector3(0.5f, 0.68f, 0), new Vector3(1.0f, 0.08f, d - 0.14f), Mats.Lit(new Color(0.3f, 0.55f, 0.6f), 0.1f));
            }
            float bedding = grade == 0 ? .417f : grade == 1 ? .495f : .535f;
            float blanket = grade == 0 ? .478f : grade == 1 ? .621f : .711f;
            var cottonEdge = PropForms.Pigment(grade == 0 ? "877A65" : grade == 1 ? "6D8396" : "C5B9A4");
            var blanketEdge = PropForms.Pigment(grade == 0 ? "AA745B" : grade == 1 ? "738CA3" : "609597");
            for (int side = -1; side <= 1; side += 2)
            {
                Prims.Box(r,"MattressPiping",new Vector3(.05f,bedding,side*(d/2-(grade==0?.038f:grade==1?.049f:.059f))),
                    new Vector3(w-.20f,.015f,.012f),cottonEdge);
                Prims.Box(r,"BlanketHem",new Vector3(.48f,blanket,side*(d/2-(grade==0?.052f:grade==1?.062f:.070f))),
                    new Vector3(.91f,.013f,.019f),blanketEdge);
                if (grade > 0)
                    Prims.Box(r,"BedRail",new Vector3(0,.156f,side*d*.49f),new Vector3(w-.08f,.058f,.041f),PropForms.Wood("6B4C37"));
            }
            if (district == 0 && grade == 0)
                Prims.Box(r,"EdgeRepair",new Vector3(.44f,.33f,-d/2-.002f),new Vector3(.16f,.025f,.007f),PropForms.Pigment("956747"));
        }

        // ---------- 衣柜 ----------

        // ---------- 小桌与电饭锅 ----------

        private static void TableCooker(Transform r, float w, float d, bool c)
        {
            Prims.Box(r, "Top", new Vector3(0, 0.72f, 0), new Vector3(w, 0.05f, d), PropForms.Wood("9A764F"), c);
            foreach (Vector2 p in new[] { new Vector2(-w / 2 + 0.06f, -d / 2 + 0.06f), new Vector2(w / 2 - 0.06f, -d / 2 + 0.06f), new Vector2(-w / 2 + 0.06f, d / 2 - 0.06f), new Vector2(w / 2 - 0.06f, d / 2 - 0.06f) })
                Prims.Box(r, "Leg", new Vector3(p.x, 0.36f, p.y), new Vector3(0.04f, 0.72f, 0.04f), Mats.Lit(new Color(0.3f, 0.3f, 0.32f), 0.3f, 0.5f));
            Prims.Shape(PrimitiveType.Cylinder, r, "RiceCooker", new Vector3(-0.35f, 0.88f, 0), new Vector3(0.34f, 0.15f, 0.34f), Mats.Lit(new Color(0.86f, 0.86f, 0.8f), 0.3f), c);
            var lid = new GameObject("CookerLid").transform; lid.SetParent(r, false); lid.localPosition = new Vector3(-.35f, 1.045f, 0);
            Prims.Shape(PrimitiveType.Cylinder, lid, "Lid", Vector3.zero, new Vector3(.35f, .015f, .35f), Mats.Lit(new Color(.65f, .69f, .66f), .5f));
            Prims.Box(lid, "Handle", new Vector3(0, .031f, 0), new Vector3(.11f, .025f, .045f), Mats.Lit(new Color(.2f, .24f, .22f)));
            var light = Prims.Box(r, "CookerLight", new Vector3(-.35f, .93f, -.17f), new Vector3(.075f, .035f, .025f), Mats.Glow(new Color(.18f, .2f, .17f), Color.black, false));
            Prims.Box(r,"TableApron",new Vector3(0,.662f,-d*.42f),new Vector3(w-.075f,.047f,.056f),PropForms.Wood("795A3D"));
            PropForms.Tube(r,"锅底胶圈",new Vector3(-.35f,.749f,0),new Vector3(-.35f,.762f,0),.313f,PropForms.Ink);
            PropForms.Tube(r,"锅身接口",new Vector3(-.35f,1.004f,0),new Vector3(-.35f,1.017f,0),.337f,PropForms.Pigment("A6A598"));
            var animation = r.gameObject.AddComponent<RiceCookerVisual>();
            animation.Lid = lid; animation.Indicator = light.GetComponent<Renderer>(); animation.Steam = new Renderer[5];
            for (int i = 0; i < animation.Steam.Length; i++)
            {
                var steam = Prims.Shape(PrimitiveType.Sphere, r, "CookerSteam" + i, Vector3.zero, Vector3.one * .03f, Mats.Unlit(new Color(.94f, .97f, 1f, .3f)));
                var renderer = steam.GetComponent<Renderer>(); renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
                animation.Steam[i] = renderer; steam.SetActive(false);
            }
            Prims.Shape(PrimitiveType.Cylinder, r, "Bowl", new Vector3(0.25f, 0.77f, 0f), new Vector3(0.2f, 0.04f, 0.2f), Mats.Lit(new Color(0.9f, 0.9f, 0.92f), 0.4f));
            Prims.Shape(PrimitiveType.Cylinder, r, "Mug", new Vector3(0.5f, 0.8f, 0.08f), new Vector3(0.1f, 0.06f, 0.1f), Mats.Lit(new Color(0.3f, 0.5f, 0.7f), 0.4f));
        }

        // ---------- 杂物 ----------

        private static void Stool(Transform root, bool collider)
        {
            const float seatDiameter = .34f;
            var redPlastic = Mats.Lit(new Color(.68f, .22f, .18f), .22f);
            var seatTop = Mats.Lit(new Color(.73f, .27f, .22f), .26f);
            Prims.Shape(PrimitiveType.Cylinder, root, "SeatRim", new Vector3(0, StoolSeatHeight - .029f, 0),
                new Vector3(seatDiameter, .021f, seatDiameter), redPlastic);
            Prims.Shape(PrimitiveType.Cylinder, root, "SeatTop", new Vector3(0, StoolSeatHeight - .012f, 0),
                new Vector3(.323f, .012f, .323f), seatTop);
            for (int i = 0; i < 4; i++)
            {
                float angle = (i + .5f) * Mathf.PI / 2f;
                var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var foot = outward * .157f + Vector3.up * .004f;
                var shoulder = outward * .113f + Vector3.up * (StoolSeatHeight - .043f);
                Vector3 axis = shoulder - foot;
                var leg = Prims.Box(root, "Leg", (foot + shoulder) * .5f, new Vector3(.052f, axis.magnitude, .052f), redPlastic);
                leg.transform.localRotation = Quaternion.FromToRotation(Vector3.up, axis);
            }

            foreach (int side in new[] { -1, 1 })
            {
                PropForms.Box(root, "MouldedBrace", side * .096f, .151f, 0, .032f, .039f, .191f, redPlastic);
                PropForms.Box(root, "MouldedBrace", 0, .151f, side * .096f, .191f, .039f, .032f, redPlastic);
            }
            PropForms.Box(root, "SeatEdgeScuff", -.072f, StoolSeatHeight - .008f, -.133f, .033f, .004f, .009f, PropForms.Pigment("AD7760"));

            if (collider)
            {
                var hit = root.gameObject.AddComponent<CapsuleCollider>();
                hit.center = Vector3.up * (StoolSeatHeight / 2f);
                hit.height = StoolSeatHeight;
                hit.radius = seatDiameter / 2f;
            }
        }

        private static void Fan(Transform root, bool collider, bool broken)
        {
            var housing = PropForms.Pigment(broken ? "96917E" : "A4A7A7");
            var metal = Mats.Lit(broken ? ArtDirection.Hex("777363") : ArtDirection.Hex("868A8D"), .22f, .26f);
            var grille = Mats.Lit(new Color(.56f, .56f, .58f), .27f, .32f);
            var blades = Mats.Lit(new Color(.69f, .72f, .73f), .23f);
            Prims.Shape(PrimitiveType.Cylinder, root, "Base", new Vector3(0, .028f, 0),
                new Vector3(.38f, .027f, .38f), metal);
            if (collider)
            {
                var hit = root.gameObject.AddComponent<BoxCollider>();
                hit.center = Vector3.up * .03f;
                hit.size = new Vector3(.38f, .05f, .38f);
            }
            Prims.Shape(PrimitiveType.Cylinder, root, "BaseTop", new Vector3(0, .059f, 0),
                new Vector3(.33f, .014f, .33f), housing);
            Prims.Shape(PrimitiveType.Cylinder, root, "LowerPole", new Vector3(0, .32f, 0),
                new Vector3(.052f, .27f, .052f), housing);
            Prims.Shape(PrimitiveType.Cylinder, root, "UpperPole", new Vector3(0, .81f, 0),
                new Vector3(.035f, .28f, .035f), metal);
            Prims.Box(root, "Controls", new Vector3(0, .62f, -.012f), new Vector3(.061f, .15f, .056f), housing);
            for (int i = 0; i < 3; i++)
                Prims.Box(root, "SpeedButton", new Vector3(0, .58f + i * .037f, -.043f), new Vector3(.028f, .018f, .014f), metal);

            var head = new GameObject("Head").transform;
            head.SetParent(root, false);
            head.localPosition = new Vector3(0, 1.1f, 0);
            head.localRotation = broken ? Quaternion.Euler(-25f, 0, 0) : Quaternion.identity;
            var motor = Prims.Shape(PrimitiveType.Cylinder, head, "MotorHousing", new Vector3(0, 0, .057f),
                new Vector3(.158f, .056f, .158f), housing);
            motor.transform.localRotation = Quaternion.Euler(90f, 0, 0);
            for (int i = 0; i < 3; i++)
            {
                float angle = 120f * i;
                var direction = Quaternion.Euler(0, 0, angle);
                var blade = Prims.Shape(PrimitiveType.Sphere, head, "Blade", direction * new Vector3(.109f, 0, -.007f),
                    new Vector3(.178f, .090f, .018f), blades);
                blade.transform.localRotation = Quaternion.Euler(0, 0, angle + 25f);
            }
            var hub = Prims.Shape(PrimitiveType.Cylinder, head, "CenterCap", new Vector3(0, 0, -.071f),
                new Vector3(.106f, .013f, .106f), housing);
            hub.transform.localRotation = Quaternion.Euler(90f, 0, 0);

            // Build the guard as one continuous mesh so the blades stay visible through the wires.
            var guard = new MiniatureDetailMesh();
            var wire = guard.Part(grille);
            const int segments = 24;
            const float rimRadius = .212f;
            const float frontRim = -.038f;
            const float backRim = .034f;
            for (int step = 0; step < segments; step++)
            {
                float angle = step * (2f * Mathf.PI / segments);
                var a = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
                var b = new Vector3(Mathf.Cos(angle + 2f * Mathf.PI / segments), Mathf.Sin(angle + 2f * Mathf.PI / segments), 0);
                RingSegment(rimRadius, frontRim, .007f);
                RingSegment(rimRadius, backRim, .005f);
                RingSegment(.151f, -.052f, .0035f);
                RingSegment(.089f, -.063f, .0035f);
                if (step % 2 == 0)
                    AddWire(a * .046f + Vector3.forward * -.075f, a * rimRadius + Vector3.forward * frontRim, .0037f);
                if (step % 4 == 0)
                {
                    AddWire(a * .061f + Vector3.forward * backRim, a * rimRadius + Vector3.forward * backRim, .004f);
                    AddWire(a * rimRadius + Vector3.forward * frontRim, a * rimRadius + Vector3.forward * backRim, .0045f);
                }

                void RingSegment(float radius, float depth, float thickness)
                {
                    var offset = Vector3.forward * depth;
                    AddWire(a * radius + offset, b * radius + offset, thickness);
                }
            }
            guard.Build(head, "WireGuard");
            if (broken)
                PropForms.Box(root, "WornEnamelChip", -.071f, .073f, -.092f, .041f, .003f, .022f, PropForms.Pigment("927054"));

            void AddWire(Vector3 start, Vector3 end, float radius) =>
                MiniatureDetailMesh.Branch(wire.V, wire.T, start, end, radius, radius);
        }

        private static void Junk(Transform r, bool c)
        {
            FurniturePropArt.Carton(r, "LargeCarton", new Vector3(-.14f, 0, -.031f), new Vector3(.66f, .49f, .57f), c);
            FurniturePropArt.Carton(r, "SmallCarton", new Vector3(-.18f, .49f, -.075f), new Vector3(.48f, .282f, .41f), false);
            PropForms.Revolve(r, "Basin", PropForms.Pigment("A45443"), "house-basin", new Vector3(.302f, 0, -.13f),
                new Vector3(.44f, .165f, .44f), new Vector2(0, 0), new Vector2(.30f, 0), new Vector2(.50f,.89f),
                new Vector2(.488f, 1), new Vector2(.45f, .965f), new Vector2(.282f, .18f), new Vector2(0, .18f));
            PropForms.Revolve(r, "Bucket", PropForms.Pigment("587786"), "house-basin", new Vector3(.272f, 0, .263f),
                new Vector3(.29f, .262f, .29f), new Vector2(0, 0), new Vector2(.30f, 0), new Vector2(.50f,.89f),
                new Vector2(.488f, 1), new Vector2(.45f, .965f), new Vector2(.282f, .18f), new Vector2(0, .18f));
        }

        // ---------- 墙面 ----------

        private static void Poster(Transform r, float w, float h, float y, Color paper, Color accent)
        {
            Prims.Box(r, "Frame", new Vector3(0, y, -0.015f), new Vector3(w + 0.06f, h + 0.06f, 0.03f), Mats.Lit(new Color(0.15f, 0.13f, 0.12f)));
            Prims.Box(r, "Paper", new Vector3(0, y, -0.035f), new Vector3(w, h, 0.02f), Mats.Lit(paper, 0.1f));
            Prims.Box(r, "Art", new Vector3(0, y - h * 0.05f, -0.047f), new Vector3(w * 0.6f, h * 0.45f, 0.01f), Mats.Lit(accent, 0.1f));
        }

        private static void Led(Transform r, float w)
        {
            Prims.Box(r, "Frame", new Vector3(0, 2.45f, -0.05f), new Vector3(w + 0.08f, 0.5f, 0.08f), Mats.Lit(new Color(0.08f, 0.08f, 0.1f)));
            Prims.Box(r, "Screen", new Vector3(0, 2.45f, -0.1f), new Vector3(w, 0.42f, 0.02f), Mats.Glow(new Color(0.9f, 0.3f, 0.5f), new Color(2.6f, 0.7f, 1.6f), false));
        }

        // ---------- 地面装饰 ----------

        private static void Rug(Transform r, float w, float d)
        {
            PropForms.Box(r, "WovenBorder", 0, .010f, 0, w, .018f, d, PropForms.Pigment("86504C"));
            PropForms.Box(r, "WovenField", 0, .020f, 0, w - .25f, .009f, d - .25f, PropForms.Pigment("B59B74"));
            foreach (float side in new[] { -1f, 1f })
            {
                PropForms.Box(r, "BorderCord", 0, .025f, side * (d / 2 - .106f), w - .176f, .006f, .018f, PropForms.Pigment("D0AB77"));
                for (int tuft = 0; tuft < 22; tuft++)
                    PropForms.Box(r, "Tassel", (tuft - 10.5f) * (w - .14f) / 22f, .018f, side * (d / 2 - .028f),
                        .012f, .007f, .049f, PropForms.Pigment("B69774"));
            }
        }

        private static void HomeWorkbench(Transform root, float width, float depth, bool collider)
        {
            var chassis = Mats.Lit(new Color(.12f, .16f, .20f), .46f, .3f);
            var aluminum = Mats.Lit(new Color(.43f, .52f, .59f), .52f, .38f);
            var inset = Mats.Lit(new Color(.055f, .08f, .11f), .27f);
            var cyan = Mats.Glow(new Color(.05f, .45f, .54f), new Color(.12f, .95f, 1.2f), nightOnly: false);
            var amber = Mats.Glow(new Color(.75f, .42f, .12f), new Color(1f, .55f, .14f), nightOnly: false);

            Prims.Box(root, "ConsoleTop", new Vector3(0, .84f, 0), new Vector3(width, .10f, depth), chassis, collider);
            Prims.Box(root, "ConsoleTrim", new Vector3(0, .781f, 0), new Vector3(width - .035f, .048f, depth - .02f), aluminum);
            Prims.Box(root, "FrontStatusStrip", new Vector3(0, .893f, -depth / 2 + .026f), new Vector3(width - .18f, .012f, .028f), cyan);
            Prims.Box(root, "RearEquipmentRail", new Vector3(0, .94f, depth * .30f), new Vector3(width - .12f, .11f, .22f), chassis);
            Prims.Box(root, "RearStatusStrip", new Vector3(0, .996f, depth * .39f), new Vector3(width * .76f, .012f, .026f), cyan);

            float cabinetX = width * .36f;
            Prims.Box(root, "EquipmentCabinet", new Vector3(cabinetX, .43f, .035f), new Vector3(.40f, .72f, depth * .74f), chassis, collider);
            Prims.Box(root, "EquipmentFace", new Vector3(cabinetX, .44f, -.25f), new Vector3(.32f, .57f, .035f), inset);
            for (int slot = 0; slot < 4; slot++)
                Prims.Box(root, "Vent", new Vector3(cabinetX, .25f + slot * .062f, -.27f), new Vector3(.24f, .023f, .01f), aluminum);
            Prims.Box(root, "CabinetPower", new Vector3(cabinetX + .09f, .64f, -.272f), new Vector3(.054f, .027f, .013f), amber);
            Prims.Box(root, "ConsoleSupport", new Vector3(-cabinetX, .43f, .035f), new Vector3(.14f, .73f, depth * .64f), aluminum, collider);
            foreach (float x in new[] { -cabinetX, cabinetX })
                Prims.Box(root, "ConsoleFoot", new Vector3(x, .047f, 0), new Vector3(.42f, .094f, depth * .88f), chassis);

            BuildWorkbenchDisplay(root, new Vector3(0, 1.26f, .20f), .86f, .48f, 0, chassis, cyan, collider);
            BuildWorkbenchDisplay(root, new Vector3(-width * .335f, 1.19f, .15f), .45f, .36f, -15, chassis, cyan, collider);
            BuildWorkbenchDisplay(root, new Vector3(width * .335f, 1.19f, .15f), .45f, .36f, 15, chassis, amber, collider);

            Prims.Box(root, "KeyboardDeck", new Vector3(-.11f, .911f, -.16f), new Vector3(.56f, .042f, .24f), inset);
            for (int row = 0; row < 3; row++)
                Prims.Box(root, "KeyboardRow", new Vector3(-.11f, .934f, -.23f + row * .062f), new Vector3(.44f, .009f, .028f), aluminum);
            Prims.Box(root, "Touchpad", new Vector3(.39f, .908f, -.17f), new Vector3(.26f, .027f, .23f), aluminum);
            Prims.Box(root, "TouchpadSurface", new Vector3(.39f, .923f, -.17f), new Vector3(.21f, .008f, .18f), inset);
            Prims.Box(root, "ControlKey", new Vector3(.73f, .907f, -.17f), new Vector3(.095f, .033f, .07f), cyan);
        }

        private static void BuildWorkbenchDisplay(Transform root, Vector3 center, float width, float height, float yaw,
            Material chassis, Material accent, bool collider)
        {
            var display = new GameObject("Display").transform;
            display.SetParent(root, false);
            display.localPosition = center;
            display.localRotation = Quaternion.Euler(12, yaw, 0);
            float mountHeight = center.y - .98f;
            Prims.Box(root, "DisplayMount", new Vector3(center.x, .98f + mountHeight / 2, center.z),
                new Vector3(.10f, mountHeight, .09f), chassis);
            Prims.Box(display, "DisplayHousing", Vector3.zero, new Vector3(width, height, .075f), chassis, collider);
            Prims.Box(display, "RearIndicator", new Vector3(0, height * .25f, .039f), new Vector3(width * .52f, .019f, .009f), accent);
            var screen = Mats.Unlit(new Color(.036f, .13f, .19f));
            var grid = Mats.Unlit(new Color(.12f, .30f, .38f));
            Prims.Box(display, "Screen", new Vector3(0, .006f, -.042f), new Vector3(width - .055f, height - .055f, .01f), screen);
            Prims.Box(display, "ScreenHeader", new Vector3(0, height * .31f, -.05f), new Vector3(width * .76f, .025f, .006f), accent);
            for (int row = 0; row < 3; row++)
                Prims.Box(display, "ChartGrid", new Vector3(0, (row - 1) * height * .17f, -.05f), new Vector3(width * .76f, .012f, .006f), grid);
            for (int column = 0; column < 5; column++)
            {
                float barHeight = height * (.16f + ((column * 3 + 1) % 5) * .065f);
                Prims.Box(display, "ChartBar", new Vector3((column - 2) * width * .13f, -.22f * height + barHeight / 2, -.055f),
                    new Vector3(width * .075f, barHeight, .007f), accent);
            }
        }

        private static void Desk(Transform r, float w, float d, bool c, bool withLaptop = true, int district = 0)
        {
            if (district == 3 && withLaptop)
            {
                var body = PropForms.Pigment("BABBB5");
                var edge = PropForms.Pigment("7F8381");
                PropForms.Box(r,"DeskTop",new Vector3(0,.754f,0),new Vector3(w,.074f,d),body,c);
                foreach (int side in new[] {-1,1})
                    PropForms.Box(r,"DeskEnd",side*(w/2-.053f),.356f,0,.092f,.712f,d-.034f,body);
                PropForms.Box(r,"DeskStretcher",0,.547f,.46f*d,w-.134f,.25f,.051f,edge);
                var display = Mats.Glow(ArtDirection.Hex("80ADB6"),new Color(.18f,.56f,.70f),false);
                PropForms.Box(r,"DeskDock",0,.809f,.221f,.592f,.041f,.096f,edge);
                PropForms.Box(r,"ThinDisplay",0,1.079f,.224f,.659f,.508f,.023f, display);
                for (int i=0;i<3;i++)
                    PropForms.Box(r,"DisplayInterface",-.15f,1.198f-i*.075f,.21f,.225f-i*.041f,.016f,.007f,PropForms.Cream);
                PropForms.Box(r,"Keyboard",0,.798f,-.113f,.353f,.023f,.145f,edge);
                return;
            }
            Prims.Box(r, "Top", new Vector3(0, 0.74f, 0), new Vector3(w, 0.05f, d), FurniturePropArt.TexturedWood(district), c);
            foreach (Vector2 p in new[] { new Vector2(-w / 2 + 0.05f, -d / 2 + 0.05f), new Vector2(w / 2 - 0.05f, -d / 2 + 0.05f), new Vector2(-w / 2 + 0.05f, d / 2 - 0.05f), new Vector2(w / 2 - 0.05f, d / 2 - 0.05f) })
                Prims.Box(r, "Leg", new Vector3(p.x, 0.36f, p.y), new Vector3(0.05f, 0.72f, 0.05f), Mats.Lit(new Color(0.3f, 0.3f, 0.32f), 0.3f, 0.5f));
            Prims.Box(r,"DeskApron",new Vector3(0,.683f,d*.388f),new Vector3(w-.10f,.059f,.038f),FurniturePropArt.TexturedWood(district));
            if (!withLaptop) return;
            Prims.Box(r, "Laptop", new Vector3(0, 0.78f, 0.05f), new Vector3(0.34f, 0.02f, 0.24f), Mats.Lit(new Color(0.2f, 0.2f, 0.23f), 0.4f, 0.4f));
            var screen = Prims.Box(r, "Screen", new Vector3(0,.895f,.159f), new Vector3(.343f,.226f,.022f),PropForms.Ink);
            screen.transform.localRotation=Quaternion.Euler(13,0,0);
            Prims.Box(screen.transform,"Display",new Vector3(0,.028f,-.58f),new Vector3(.91f,.86f,.075f),
                Mats.Glow(ArtDirection.Hex("314856"),new Color(.36f,.73f,.88f),false));
            for(int row=0;row<4;row++)
                Prims.Box(r,"Keyboard",new Vector3(0,.793f,-.022f+row*.031f),new Vector3(.26f,.007f,.019f),PropForms.Pigment("5D5D5B"));
            Prims.Box(r,"Trackpad",new Vector3(0,.793f,-.054f),new Vector3(.097f,.006f,.032f),PropForms.Pigment("51524F"));
        }

    }

    /// <summary>
    /// 一个房间里摆出来的全部家具模型：布局变了才重建。
    /// 同时给出地面家具的占地（走路挡路）和互动点（床、衣柜、电饭锅）。
    /// </summary>
    public sealed class RoomFurniture
    {
        private readonly Transform _parent;
        private readonly int _district;
        private readonly List<GameObject> _items = new List<GameObject>();
        private readonly List<PlacedFurniture> _snapshot = new List<PlacedFurniture>();
        private double _roomWidth = -1, _roomDepth = -1;
        private bool _roomIsShop;

        /// <summary>地面家具的占地（走路用，x/y = 房间局部 x/z）。</summary>
        public readonly List<Rect> Blockers = new List<Rect>();

        /// <summary>摆出来的每一件：模型、定义、摆放（店内场景从里面找货架、收银台这些设施）。</summary>
        public sealed class Entry
        {
            public GameObject Go;
            public FurnitureDef Def;
            public PlacedFurniture Placed;
            /// <summary>地面家具的占地（房间局部 x/z）；墙面、薄地面没有。</summary>
            public Rect Foot;
            public bool HasFoot;
        }

        public readonly List<Entry> Entries = new List<Entry>();

        /// <summary>布局重建过几次（店内场景据此刷新设施数据）。</summary>
        public int Version { get; private set; }

        public RoomFurniture(Transform parent, int district = 0)
        {
            _parent = parent;
            _district = district;
        }

        /// <summary>布局有变化就重建，返回是否重建了。</summary>
        public bool Sync(IReadOnlyList<PlacedFurniture> layout, RoomSpec room)
        {
            bool same = layout.Count == _snapshot.Count && room.Width == _roomWidth
                && room.Depth == _roomDepth && room.IsShop == _roomIsShop;
            for (int i = 0; same && i < layout.Count; i++)
            {
                var a = layout[i]; var b = _snapshot[i];
                same = a.Id == b.Id && a.X == b.X && a.Z == b.Z && a.Rot == b.Rot
                    && a.Item == b.Item && a.DisplayLimit == b.DisplayLimit && a.ClimateId == b.ClimateId;
            }
            if (same) return false;
            _snapshot.Clear();
            for (int i = 0; i < layout.Count; i++) _snapshot.Add(layout[i].Clone());
            _roomWidth = room.Width; _roomDepth = room.Depth; _roomIsShop = room.IsShop;

            foreach (GameObject go in _items) Kill.It(go);
            _items.Clear();
            Blockers.Clear();
            Entries.Clear();
            Version++;
            for (int i = 0; i < layout.Count; i++)
            {
                FurnitureDef def = FurnitureCatalog.Get(layout[i].Id);
                if (def == null) continue;
                GameObject go = FurnitureVisuals.Build(_parent, def, true, layout[i].Item, _district);
                FurnitureVisuals.Place(go, def, layout[i], room);
                var pv = go.AddComponent<PlacedVisual>();
                pv.Index = i;
                pv.FurnitureId = def.Id;
                _items.Add(go);
                var entry = new Entry { Go = go, Def = def, Placed = layout[i] };
                Entries.Add(entry);
                if (def.Slot == FurnitureSlot.Floor)
                {
                    RectD f = RoomPlanner.Footprint(room, def, layout[i]);
                    var foot = new Rect((float)f.MinX, (float)f.MinZ, (float)(f.MaxX - f.MinX), (float)(f.MaxZ - f.MinZ));
                    Blockers.Add(foot);
                    entry.Foot = foot;
                    entry.HasFoot = true;
                }
            }
            return true;
        }

        /// <summary>摆放模式里拾起一件时先把它藏起来。</summary>
        public void SetHidden(int index, bool hidden)
        {
            foreach (GameObject go in _items)
            {
                var pv = go.GetComponent<PlacedVisual>();
                if (pv != null && pv.Index == index) go.SetActive(!hidden);
            }
        }

        public void ShowAll()
        {
            foreach (GameObject go in _items) go.SetActive(true);
        }

        public void Destroy()
        {
            foreach (GameObject go in _items) Kill.It(go);
            _items.Clear();
            Entries.Clear();
            Blockers.Clear();
            _snapshot.Clear();
            _roomWidth = _roomDepth = -1;
        }
    }
}
