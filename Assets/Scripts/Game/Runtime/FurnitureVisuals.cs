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
    /// 所有家具的模型（积木拼的低模）。局部坐标：原点在占地中心的地面上，宽沿 X，进深沿 Z，正面朝 -Z；
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

        public static GameObject Build(Transform parent, FurnitureDef def, bool collider, string shelfKind = null)
        {
            var root = new GameObject("F_" + def.Id).transform;
            root.SetParent(parent, false);
            float w = (float)def.Width, d = (float)def.Depth;
            bool c = collider;
            if (def.IsFixture)
            {
                ShopFixtures.BuildFixture(root, def, c, shelfKind);
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
                    Prims.Box(root,"MirrorGlass",new Vector3(0,.96f,-.05f),new Vector3(.60f,1.56f,.012f),Mats.Lit(new Color(.66f,.81f,.86f),.85f));
                    Prims.Box(root,"MirrorFoot",new Vector3(0,.06f,0),new Vector3(.70f,.12f,.25f),Mats.Lit(Metal),c);
                    break;
                case FurnitureCatalog.BedBoard: Bed(root, w, d, c, 0); break;
                case "bed_spring": Bed(root, w, d, c, 1); break;
                case "bed_good": Bed(root, w, d, c, 2); break;
                case FurnitureCatalog.WardrobeCloth: Wardrobe(root, w, d, c, false); break;
                case "wardrobe_wood": Wardrobe(root, w, d, c, true); break;
                case FurnitureCatalog.FridgeSmall:
                case FurnitureCatalog.Fridge: Fridge(root, def, c); break;
                case FurnitureCatalog.TableCooker: TableCooker(root, w, d, c); break;
                case "stool": Stool(root, c); break;
                case "fan_broken": Fan(root, c, true); break;
                case "fan_new":
                case "fan_fixed": Fan(root, c, false); break;
                case "junk": Junk(root, c); break;
                case FurnitureCatalog.HomeBox:
                case FurnitureCatalog.ShopBox: Carton(root, w, d, c); break;
                case "aircon_large":
                case "aircon":
                    AirCon(root);
                    if(c){var click=root.gameObject.AddComponent<BoxCollider>();click.center=new Vector3(0,2.4f,-.15f);click.size=new Vector3(w,.5f,.35f);}
                    break;
                case FurnitureCatalog.Curtain: Curtain(root); break;
                case "poster": Poster(root, w, 1.0f, 1.55f, new Color(0.85f, 0.55f, 0.25f), new Color(0.25f, 0.45f, 0.65f)); break;
                case "rug": Rug(root, w, d); break;
                case "plant": Plant(root, 0.4f, 0.9f, c); break;
                case "shop_plant": Plant(root, 0.5f, 1.3f, c); break;
                case FurnitureCatalog.HomeWorkbench: HomeWorkbench(root, w, d, c); break;
                case "desk": Desk(root, w, d, c); break;
                case "sofa": Sofa(root, w, d, c, new Color(0.55f, 0.4f, 0.3f)); break;
                case "shop_sofa": Sofa(root, w, d, c, new Color(0.25f, 0.4f, 0.6f)); break;
                case "shop_dispenser": Dispenser(root, c); break;
                case "shop_poster": Poster(root, w, 1.3f, 2.55f, new Color(0.9f, 0.3f, 0.25f), new Color(0.95f, 0.9f, 0.4f)); break;
                case "shop_led": Led(root, w); break;
                default:
                    Prims.Box(root, "Body", new Vector3(0, 0.25f, 0), new Vector3(w, 0.5f, d), Mats.Lit(new Color(0.6f, 0.55f, 0.5f)), c);
                    break;
            }
            return root.gameObject;
        }

        private static void SpecialFurniture(Transform root,FurnitureDef def,bool collider)
        {
            float w=(float)def.Width,d=(float)def.Depth;
            if (def.Id == "restaurant_table")
            {
                RestaurantDiningSet(root, w, d, collider);
                return;
            }
            Desk(root,w,d,collider);
            if(def.Id=="restaurant_stove")
            {
                Prims.Box(root,"StoveBody",new Vector3(0,.55f,0),new Vector3(w,.6f,d),Mats.Lit(Metal),collider);
                Prims.Shape(PrimitiveType.Cylinder,root,"Wok",new Vector3(0,.94f,0),new Vector3(.5f,.07f,.5f),Mats.Lit(Metal));
                Prims.Box(root,"WokHandle",new Vector3(.34f,.96f,0),new Vector3(.32f,.04f,.06f),Mats.Lit(ArtDirection.Wood));
                Prims.Shape(PrimitiveType.Sphere,root,"FoodInWok",new Vector3(0,1.02f,0),new Vector3(.38f,.08f,.38f),Mats.Lit(new Color(.85f,.67f,.25f)));
            }
            else if(def.Id==FurnitureCatalog.LegacyRestaurantPrep)
            {
                Prims.Box(root,"ChoppingBoard",new Vector3(0,.87f,0),new Vector3(.55f,.04f,.35f),Mats.Lit(new Color(.72f,.51f,.3f)));
                Prims.Box(root,"Vegetable",new Vector3(-.15f,.92f,0),new Vector3(.2f,.07f,.13f),Mats.Lit(new Color(.25f,.55f,.25f)));
            }
            else if(def.Id==FurnitureCatalog.LegacyRestaurantPass)
                Prims.Box(root,"PassTop",new Vector3(0,1.3f,.12f),new Vector3(w,.05f,.3f),Mats.Lit(Metal));
            else if(def.Id=="used_buydesk")
            {
                Prims.Box(root,"InspectionMat",new Vector3(0,.87f,0),new Vector3(w*.8f,.025f,d*.75f),Mats.Lit(new Color(.18f,.38f,.3f)));
                Prims.Box(root,"InspectionLamp",new Vector3(w*.3f,1.05f,.15f),new Vector3(.08f,.38f,.08f),Mats.Lit(Metal));
            }
            else if(def.Id=="used_display")
                Prims.Box(root,"SalesGlass",new Vector3(0,1.03f,0),new Vector3(w,.25f,d),Mats.Lit(new Color(.55f,.72f,.7f),.3f));
            else if (def.Id == FurnitureCatalog.ModelPaintBench)
            {
                Prims.Box(root, "SprayBooth", new Vector3(0, 1.08f, d * .34f), new Vector3(w * .78f, .46f, .12f), Mats.Lit(ArtDirection.Sage));
                Prims.Box(root, "Turntable", new Vector3(0, .9f, -.04f), new Vector3(.3f, .06f, .3f), Mats.Lit(Metal));
                Prims.Box(root, "Model", new Vector3(0, 1f, -.04f), new Vector3(.14f, .14f, .13f), Mats.Lit(ArtDirection.Brass));
                for (int i = 0; i < 3; i++)
                    Prims.Box(root, "PaintCup", new Vector3(w * .25f, .93f, (i - 1) * .12f), new Vector3(.065f, .12f, .065f),
                        Mats.Lit(i == 0 ? ArtDirection.Sage : i == 1 ? ArtDirection.Brass : ArtDirection.Wood));
            }
            else if(def.Id=="used_repairbench")
            {
                Prims.Box(root,"RepairMat",new Vector3(0,.88f,0),new Vector3(w*.8f,.025f,d*.75f),Mats.Lit(ArtDirection.Sage));
                Prims.Box(root,"ToolRack",new Vector3(0,1.07f,d*.35f),new Vector3(w*.8f,.35f,.06f),Mats.Lit(ArtDirection.Wood));
                for(int i=-1;i<=1;i++)Prims.Box(root,"HandTool",new Vector3(i*w*.22f,1.1f,d*.3f),new Vector3(.045f,.24f,.045f),Mats.Lit(Metal));
                Prims.Box(root,"PartsTray",new Vector3(w*.28f,.92f,-d*.12f),new Vector3(.25f,.07f,.2f),Mats.Lit(ArtDirection.Brass));
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

        private static void Fridge(Transform root, FurnitureDef def, bool collider)
        {
            float w = (float)def.Width, d = (float)def.Depth;
            bool small = def.Id == FurnitureCatalog.FridgeSmall;
            float height = small ? 1.15f : 1.8f;
            int rows = small ? 1 : 2;
            Color body = new Color(0.8f, 0.86f, 0.85f);
            Color door = new Color(0.92f, 0.95f, 0.93f);
            Prims.Box(root, "FridgeBody", new Vector3(0, height / 2, 0), new Vector3(w, height, d), Mats.Lit(body), collider);
            for (int y = 0; y < rows; y++)
            {
                float dh = height / rows;
                float cy = dh * (y + 0.5f);
                Prims.Box(root, "FridgeDoor_0_" + y, new Vector3(0, cy, -d / 2 - 0.015f), new Vector3(w - 0.025f, dh - 0.025f, 0.04f), Mats.Lit(door), false);
                Prims.Box(root, "FridgeHandle_0_" + y, new Vector3(w / 2 - 0.09f, cy, -d / 2 - 0.065f), new Vector3(0.035f, Mathf.Min(0.28f, dh / 3), 0.05f), Mats.Lit(Metal), false);
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

        private static void Bed(Transform r, float w, float d, bool c, int grade)
        {
            float hx = -w / 2;
            if (grade == 0)
            {
                foreach (Vector2 p in new[] { new Vector2(-w / 2 + 0.1f, -d / 2 + 0.1f), new Vector2(w / 2 - 0.1f, -d / 2 + 0.1f), new Vector2(-w / 2 + 0.1f, d / 2 - 0.1f), new Vector2(w / 2 - 0.1f, d / 2 - 0.1f) })
                    Prims.Box(r, "Leg", new Vector3(p.x, 0.15f, p.y), new Vector3(0.06f, 0.3f, 0.06f), Mats.Lit(Metal, 0.3f, 0.5f));
                Prims.Box(r, "Board", new Vector3(0, 0.33f, 0), new Vector3(w, 0.06f, d), Mats.Lit(new Color(0.45f, 0.33f, 0.22f)), c);
                Prims.Box(r, "Head", new Vector3(hx + 0.04f, 0.65f, 0), new Vector3(0.08f, 0.6f, d), Mats.Lit(Metal, 0.3f, 0.5f));
                Prims.Box(r, "Mattress", new Vector3(0.1f, 0.40f, 0), new Vector3(w - 0.2f, 0.08f, d - 0.08f), Mats.Lit(new Color(0.62f, 0.6f, 0.5f), 0.02f));
                Prims.Box(r, "Stain", new Vector3(0.25f, 0.445f, 0.05f), new Vector3(0.5f, 0.01f, 0.4f), Mats.Lit(new Color(0.5f, 0.46f, 0.36f), 0.02f));
                Prims.Box(r, "Pillow", new Vector3(hx + 0.4f, 0.47f, 0), new Vector3(0.38f, 0.1f, 0.6f), Mats.Lit(new Color(0.72f, 0.7f, 0.64f), 0.02f));
                Prims.Box(r, "Blanket", new Vector3(0.5f, 0.46f, 0), new Vector3(0.95f, 0.06f, d - 0.1f), Mats.Lit(new Color(0.55f, 0.38f, 0.3f), 0.02f));
            }
            else if (grade == 1)
            {
                Prims.Box(r, "Frame", new Vector3(0, 0.22f, 0), new Vector3(w, 0.3f, d), Mats.Lit(new Color(0.4f, 0.3f, 0.22f)), c);
                Prims.Box(r, "Head", new Vector3(hx + 0.04f, 0.6f, 0), new Vector3(0.08f, 0.7f, d), Mats.Lit(new Color(0.35f, 0.26f, 0.19f)));
                Prims.Box(r, "Mattress", new Vector3(0.05f, 0.47f, 0), new Vector3(w - 0.12f, 0.22f, d - 0.1f), Mats.Lit(new Color(0.45f, 0.62f, 0.74f), 0.05f));
                Prims.Box(r, "Pillow", new Vector3(hx + 0.4f, 0.64f, 0), new Vector3(0.38f, 0.12f, 0.6f), Mats.Lit(new Color(0.9f, 0.9f, 0.88f), 0.05f));
                Prims.Box(r, "Blanket", new Vector3(0.5f, 0.6f, 0), new Vector3(0.95f, 0.07f, d - 0.12f), Mats.Lit(new Color(0.4f, 0.5f, 0.65f), 0.05f));
            }
            else
            {
                Prims.Box(r, "Frame", new Vector3(0, 0.2f, 0), new Vector3(w, 0.34f, d), Mats.Lit(new Color(0.3f, 0.2f, 0.14f), 0.2f), c);
                Prims.Box(r, "Head", new Vector3(hx + 0.05f, 0.7f, 0), new Vector3(0.1f, 1.0f, d), Mats.Lit(new Color(0.27f, 0.18f, 0.12f), 0.2f));
                Prims.Box(r, "Mattress", new Vector3(0.05f, 0.5f, 0), new Vector3(w - 0.14f, 0.28f, d - 0.12f), Mats.Lit(new Color(0.93f, 0.9f, 0.82f), 0.1f));
                Prims.Box(r, "PillowA", new Vector3(hx + 0.4f, 0.71f, -d * 0.2f), new Vector3(0.38f, 0.14f, 0.45f), Mats.Lit(new Color(0.95f, 0.95f, 0.95f), 0.1f));
                Prims.Box(r, "PillowB", new Vector3(hx + 0.4f, 0.71f, d * 0.2f), new Vector3(0.38f, 0.14f, 0.45f), Mats.Lit(new Color(0.95f, 0.95f, 0.95f), 0.1f));
                Prims.Box(r, "Blanket", new Vector3(0.5f, 0.68f, 0), new Vector3(1.0f, 0.08f, d - 0.14f), Mats.Lit(new Color(0.3f, 0.55f, 0.6f), 0.1f));
            }
        }

        // ---------- 衣柜 ----------

        private static void Wardrobe(Transform r, float w, float d, bool c, bool wood)
        {
            float h = wood ? 1.9f : 1.7f;
            Color body = wood ? new Color(0.55f, 0.38f, 0.24f) : new Color(0.48f, 0.5f, 0.56f);
            Prims.Box(r, "Body", new Vector3(0, h / 2, 0), new Vector3(w, h, d), Mats.Lit(body, wood ? 0.25f : 0.05f), c);
            float fz = -d / 2 - 0.012f;
            Color seam = wood ? new Color(0.3f, 0.2f, 0.13f) : new Color(0.25f, 0.26f, 0.3f);
            Prims.Box(r, "Seam", new Vector3(0, h / 2, fz), new Vector3(0.025f, h - 0.1f, 0.02f), Mats.Lit(seam));
            if (!wood)
            {
                Prims.Box(r, "SeamB", new Vector3(w * 0.3f, h / 2, fz), new Vector3(0.02f, h - 0.1f, 0.02f), Mats.Lit(new Color(0.3f, 0.31f, 0.35f)));
                Prims.Box(r, "Top", new Vector3(0.05f, h + 0.08f, 0), new Vector3(w * 0.7f, 0.16f, d * 0.7f), Mats.Lit(new Color(0.68f, 0.58f, 0.4f)));
            }
            Color hc = new Color(0.75f, 0.75f, 0.78f);
            Prims.Box(r, "HandleL", new Vector3(-0.08f, h * 0.5f, fz - 0.015f), new Vector3(0.03f, 0.18f, 0.03f), Mats.Lit(hc, 0.5f, 0.6f));
            Prims.Box(r, "HandleR", new Vector3(0.08f, h * 0.5f, fz - 0.015f), new Vector3(0.03f, 0.18f, 0.03f), Mats.Lit(hc, 0.5f, 0.6f));
        }

        // ---------- 小桌与电饭锅 ----------

        private static void TableCooker(Transform r, float w, float d, bool c)
        {
            Color wood = new Color(0.55f, 0.45f, 0.32f);
            Prims.Box(r, "Top", new Vector3(0, 0.72f, 0), new Vector3(w, 0.05f, d), Mats.Lit(wood, 0.1f), c);
            foreach (Vector2 p in new[] { new Vector2(-w / 2 + 0.06f, -d / 2 + 0.06f), new Vector2(w / 2 - 0.06f, -d / 2 + 0.06f), new Vector2(-w / 2 + 0.06f, d / 2 - 0.06f), new Vector2(w / 2 - 0.06f, d / 2 - 0.06f) })
                Prims.Box(r, "Leg", new Vector3(p.x, 0.36f, p.y), new Vector3(0.04f, 0.72f, 0.04f), Mats.Lit(new Color(0.3f, 0.3f, 0.32f), 0.3f, 0.5f));
            Prims.Shape(PrimitiveType.Cylinder, r, "RiceCooker", new Vector3(-0.35f, 0.88f, 0), new Vector3(0.34f, 0.15f, 0.34f), Mats.Lit(new Color(0.86f, 0.86f, 0.8f), 0.3f), c);
            var lid = new GameObject("CookerLid").transform; lid.SetParent(r, false); lid.localPosition = new Vector3(-.35f, 1.045f, 0);
            Prims.Shape(PrimitiveType.Cylinder, lid, "Lid", Vector3.zero, new Vector3(.35f, .015f, .35f), Mats.Lit(new Color(.65f, .69f, .66f), .5f));
            Prims.Box(lid, "Handle", new Vector3(0, .031f, 0), new Vector3(.11f, .025f, .045f), Mats.Lit(new Color(.2f, .24f, .22f)));
            var light = Prims.Box(r, "CookerLight", new Vector3(-.35f, .93f, -.17f), new Vector3(.075f, .035f, .025f), Mats.Glow(new Color(.18f, .2f, .17f), Color.black, false));
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
            var housing = Mats.Lit(new Color(.60f, .60f, .62f), .18f);
            var metal = Mats.Lit(new Color(.47f, .48f, .50f), .28f, .35f);
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

            void AddWire(Vector3 start, Vector3 end, float radius) =>
                MiniatureDetailMesh.Branch(wire.V, wire.T, start, end, radius, radius);
        }

        private static void Junk(Transform r, bool c)
        {
            Prims.Box(r, "Box1", new Vector3(-0.1f, 0.28f, -0.05f), new Vector3(0.7f, 0.56f, 0.6f), Mats.Lit(new Color(0.7f, 0.58f, 0.4f), 0.02f), c);
            Prims.Box(r, "Box2", new Vector3(-0.2f, 0.7f, -0.08f), new Vector3(0.5f, 0.28f, 0.45f), Mats.Lit(new Color(0.66f, 0.54f, 0.37f), 0.02f));
            Prims.Shape(PrimitiveType.Cylinder, r, "Basin", new Vector3(0.3f, 0.1f, -0.2f), new Vector3(0.45f, 0.1f, 0.45f), Mats.Lit(new Color(0.8f, 0.3f, 0.25f), 0.3f));
            Prims.Shape(PrimitiveType.Cylinder, r, "Bucket", new Vector3(0.28f, 0.2f, 0.28f), new Vector3(0.3f, 0.2f, 0.3f), Mats.Lit(new Color(0.25f, 0.45f, 0.7f), 0.3f));
        }

        private static void Carton(Transform r, float w, float d, bool c)
        {
            Prims.Box(r, "Box", new Vector3(0, 0.25f, 0), new Vector3(w - 0.04f, 0.5f, d - 0.04f), Mats.Lit(new Color(0.72f, 0.58f, 0.38f), 0.02f), c);
            Prims.Box(r, "Tape", new Vector3(0, 0.505f, 0), new Vector3(0.08f, 0.01f, d - 0.03f), Mats.Lit(new Color(0.85f, 0.78f, 0.6f), 0.3f));
            Prims.Box(r, "Flap", new Vector3(0, 0.25f, -d / 2 + 0.015f), new Vector3(w * 0.4f, 0.12f, 0.01f), Mats.Lit(new Color(0.6f, 0.47f, 0.3f), 0.02f));
        }

        // ---------- 墙面 ----------

        private static void AirCon(Transform r)
        {
            Prims.Box(r, "Body", new Vector3(0, 2.4f, -0.15f), new Vector3(1.1f, 0.3f, 0.25f), Mats.Lit(new Color(0.88f, 0.88f, 0.86f), 0.3f));
            Prims.Box(r, "Led", new Vector3(0.35f, 2.4f, -0.285f), new Vector3(0.06f, 0.03f, 0.02f), Mats.Glow(new Color(0.1f, 0.4f, 0.1f), new Color(0.3f, 3f, 0.6f), false));
            Prims.Box(r, "Vent", new Vector3(0, 2.3f, -0.22f), new Vector3(0.9f, 0.03f, 0.15f), Mats.Lit(new Color(0.7f, 0.7f, 0.7f)));
        }

        private static void Curtain(Transform r)
        {
            Material cm = Mats.Lit(new Color(0.6f, 0.4f, 0.45f), 0.02f);
            Prims.Box(r, "PanelL", new Vector3(-0.6f, 1.75f, -0.07f), new Vector3(0.3f, 1.4f, 0.04f), cm);
            Prims.Box(r, "PanelR", new Vector3(0.6f, 1.75f, -0.07f), new Vector3(0.3f, 1.4f, 0.04f), cm);
            Prims.Box(r, "Rod", new Vector3(0, 2.45f, -0.07f), new Vector3(1.5f, 0.03f, 0.03f), Mats.Lit(new Color(0.3f, 0.3f, 0.32f), 0.3f, 0.5f));
        }

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
            Prims.Box(r, "Rug", new Vector3(0, 0.012f, 0), new Vector3(w, 0.02f, d), Mats.Lit(new Color(0.55f, 0.28f, 0.28f), 0.02f));
            Prims.Box(r, "RugIn", new Vector3(0, 0.024f, 0), new Vector3(w - 0.3f, 0.01f, d - 0.3f), Mats.Lit(new Color(0.78f, 0.62f, 0.4f), 0.02f));
        }

        private static void Plant(Transform r, float size, float height, bool c)
        {
            Prims.Shape(PrimitiveType.Cylinder, r, "Pot", new Vector3(0, 0.15f, 0), new Vector3(size * 0.7f, 0.15f, size * 0.7f), Mats.Lit(new Color(0.7f, 0.4f, 0.28f), 0.2f), c);
            Prims.Shape(PrimitiveType.Sphere, r, "Leaves", new Vector3(0, height * 0.55f, 0), new Vector3(size * 1.1f, height * 0.6f, size * 1.1f), Mats.Lit(new Color(0.25f, 0.55f, 0.28f), 0.1f));
            Prims.Shape(PrimitiveType.Sphere, r, "LeavesB", new Vector3(size * 0.15f, height * 0.85f, 0), new Vector3(size * 0.7f, height * 0.4f, size * 0.7f), Mats.Lit(new Color(0.3f, 0.62f, 0.32f), 0.1f));
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

        private static void Desk(Transform r, float w, float d, bool c, bool withLaptop = true)
        {
            Prims.Box(r, "Top", new Vector3(0, 0.74f, 0), new Vector3(w, 0.05f, d), Mats.Lit(new Color(0.6f, 0.5f, 0.38f), 0.15f), c);
            foreach (Vector2 p in new[] { new Vector2(-w / 2 + 0.05f, -d / 2 + 0.05f), new Vector2(w / 2 - 0.05f, -d / 2 + 0.05f), new Vector2(-w / 2 + 0.05f, d / 2 - 0.05f), new Vector2(w / 2 - 0.05f, d / 2 - 0.05f) })
                Prims.Box(r, "Leg", new Vector3(p.x, 0.36f, p.y), new Vector3(0.05f, 0.72f, 0.05f), Mats.Lit(new Color(0.3f, 0.3f, 0.32f), 0.3f, 0.5f));
            if (!withLaptop) return;
            Prims.Box(r, "Laptop", new Vector3(0, 0.78f, 0.05f), new Vector3(0.34f, 0.02f, 0.24f), Mats.Lit(new Color(0.2f, 0.2f, 0.23f), 0.4f, 0.4f));
            Prims.Box(r, "Screen", new Vector3(0, 0.92f, 0.17f), new Vector3(0.34f, 0.24f, 0.015f), Mats.Glow(new Color(0.1f, 0.1f, 0.12f), new Color(0.3f, 0.8f, 1.5f), false));
        }

        private static void Sofa(Transform r, float w, float d, bool c, Color color)
        {
            Material m = Mats.Lit(color, 0.1f);
            Material dark = Mats.Lit(color * 0.82f, 0.1f);
            Prims.Box(r, "Seat", new Vector3(0, SofaSeatHeight - .2f, 0), new Vector3(w, .4f, d), m, c);
            Prims.Box(r, "Back", new Vector3(0, 0.6f, d / 2 - 0.1f), new Vector3(w, 0.5f, 0.2f), dark);
            Prims.Box(r, "ArmL", new Vector3(-w / 2 + 0.1f, 0.45f, 0), new Vector3(0.2f, 0.4f, d), dark);
            Prims.Box(r, "ArmR", new Vector3(w / 2 - 0.1f, 0.45f, 0), new Vector3(0.2f, 0.4f, d), dark);
        }

        private static void Dispenser(Transform r, bool c)
        {
            Prims.Box(r, "Body", new Vector3(0, 0.5f, 0), new Vector3(0.36f, 1.0f, 0.36f), Mats.Lit(new Color(0.88f, 0.9f, 0.92f), 0.3f), c);
            Prims.Shape(PrimitiveType.Cylinder, r, "Jug", new Vector3(0, 1.25f, 0), new Vector3(0.3f, 0.25f, 0.3f), Mats.Lit(new Color(0.5f, 0.75f, 0.95f), 0.5f));
            Prims.Box(r, "Tap", new Vector3(0, 0.72f, -0.19f), new Vector3(0.08f, 0.06f, 0.05f), Mats.Lit(new Color(0.2f, 0.4f, 0.8f)));
        }
    }

    /// <summary>
    /// 一个房间里摆出来的全部家具模型：布局变了才重建。
    /// 同时给出地面家具的占地（走路挡路）和互动点（床、衣柜、电饭锅）。
    /// </summary>
    public sealed class RoomFurniture
    {
        private readonly Transform _parent;
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

        public RoomFurniture(Transform parent)
        {
            _parent = parent;
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
                GameObject go = FurnitureVisuals.Build(_parent, def, true, layout[i].Item);
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
