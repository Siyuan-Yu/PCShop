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
            if(def.Id.StartsWith("restaurant_")||def.Id.StartsWith("used_")){SpecialFurniture(root,def,c);return root.gameObject;}
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
                case FurnitureCatalog.Fridge:
                case FurnitureCatalog.FridgeSide:
                case FurnitureCatalog.FridgePremium: Fridge(root, def, c); break;
                case FurnitureCatalog.TableCooker: TableCooker(root, w, d, c); break;
                case "stool":
                    Prims.Shape(PrimitiveType.Cylinder, root, "Stool", new Vector3(0, 0.2f, 0), new Vector3(0.34f, 0.2f, 0.34f), Mats.Lit(new Color(0.7f, 0.25f, 0.2f), 0.3f), c);
                    break;
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
            Desk(root,w,d,collider);
            if(def.Id=="restaurant_stove")
            {
                Prims.Box(root,"StoveBody",new Vector3(0,.55f,0),new Vector3(w,.6f,d),Mats.Lit(Metal),collider);
                Prims.Shape(PrimitiveType.Cylinder,root,"Wok",new Vector3(0,.94f,0),new Vector3(.5f,.07f,.5f),Mats.Lit(Metal));
                Prims.Box(root,"WokHandle",new Vector3(.34f,.96f,0),new Vector3(.32f,.04f,.06f),Mats.Lit(ArtDirection.Wood));
                Prims.Shape(PrimitiveType.Sphere,root,"FoodInWok",new Vector3(0,1.02f,0),new Vector3(.38f,.08f,.38f),Mats.Lit(new Color(.85f,.67f,.25f)));
            }
            else if(def.Id=="restaurant_table")
            {
                Prims.Box(root,"DiningChair",new Vector3(0,.25f,-d*.6f),new Vector3(.38f,.5f,.35f),Mats.Lit(ArtDirection.Wood),collider);
                Prims.Shape(PrimitiveType.Cylinder,root,"DiningPlate",new Vector3(0,.88f,0),new Vector3(.3f,.015f,.3f),Mats.Lit(Color.white));
            }
            else if(def.Id=="restaurant_prep")
            {
                Prims.Box(root,"ChoppingBoard",new Vector3(0,.87f,0),new Vector3(.55f,.04f,.35f),Mats.Lit(new Color(.72f,.51f,.3f)));
                Prims.Box(root,"Vegetable",new Vector3(-.15f,.92f,0),new Vector3(.2f,.07f,.13f),Mats.Lit(new Color(.25f,.55f,.25f)));
            }
            else if(def.Id=="restaurant_pass")
                Prims.Box(root,"PassTop",new Vector3(0,1.3f,.12f),new Vector3(w,.05f,.3f),Mats.Lit(Metal));
            else if(def.Id=="used_buydesk")
            {
                Prims.Box(root,"InspectionMat",new Vector3(0,.87f,0),new Vector3(w*.8f,.025f,d*.75f),Mats.Lit(new Color(.18f,.38f,.3f)));
                Prims.Box(root,"InspectionLamp",new Vector3(w*.3f,1.05f,.15f),new Vector3(.08f,.38f,.08f),Mats.Lit(Metal));
            }
            else if(def.Id=="used_display")
                Prims.Box(root,"SalesGlass",new Vector3(0,1.03f,0),new Vector3(w,.25f,d),Mats.Lit(new Color(.55f,.72f,.7f),.3f));
        }

        private static void Fridge(Transform root, FurnitureDef def, bool collider)
        {
            float w = (float)def.Width, d = (float)def.Depth;
            bool small = def.Id == FurnitureCatalog.FridgeSmall;
            bool side = def.Id == FurnitureCatalog.FridgeSide;
            bool premium = def.Id == FurnitureCatalog.FridgePremium;
            float height = small ? 1.15f : premium ? 1.95f : 1.8f;
            int columns = side || premium ? 2 : 1;
            int rows = small || side ? 1 : 2;
            Color body = premium ? new Color(0.27f, 0.31f, 0.33f) : new Color(0.8f, 0.86f, 0.85f);
            Color door = premium ? new Color(0.42f, 0.47f, 0.48f) : side ? new Color(0.7f, 0.76f, 0.77f) : new Color(0.92f, 0.95f, 0.93f);
            Prims.Box(root, "FridgeBody", new Vector3(0, height / 2, 0), new Vector3(w, height, d), Mats.Lit(body), collider);
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < columns; x++)
                {
                    float dw = w / columns, dh = height / rows;
                    float cx = -w / 2 + dw * (x + 0.5f), cy = dh * (y + 0.5f);
                    Prims.Box(root, "FridgeDoor_" + x + "_" + y, new Vector3(cx, cy, -d / 2 - 0.015f), new Vector3(dw - 0.025f, dh - 0.025f, 0.04f), Mats.Lit(door), false);
                    float hx = columns == 1 ? w / 2 - 0.09f : x == 0 ? -0.045f : 0.045f;
                    Prims.Box(root, "FridgeHandle_" + x + "_" + y, new Vector3(hx, cy, -d / 2 - 0.065f), new Vector3(0.035f, Mathf.Min(0.28f, dh / 3), 0.05f), Mats.Lit(Metal), false);
                }
            if (premium)
                Prims.Box(root, "FridgeDisplay", new Vector3(-w * 0.23f, height * 0.78f, -d / 2 - 0.04f), new Vector3(0.11f, 0.17f, 0.015f), Mats.Glow(new Color(0.12f, 0.4f, 0.5f), new Color(0.1f, 0.8f, 1f), false));
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

        private static void Fan(Transform r, bool c, bool broken)
        {
            Prims.Box(r, "Base", new Vector3(0, 0.03f, 0), new Vector3(0.38f, 0.05f, 0.38f), Mats.Lit(new Color(0.5f, 0.5f, 0.53f), 0.2f, 0.3f), c);
            Prims.Box(r, "Pole", new Vector3(0, 0.55f, 0), new Vector3(0.05f, 1.0f, 0.05f), Mats.Lit(new Color(0.55f, 0.55f, 0.58f), 0.3f, 0.4f));
            var head = new GameObject("Head").transform;
            head.SetParent(r, false);
            head.localPosition = new Vector3(0, 1.1f, 0);
            head.localRotation = broken ? Quaternion.Euler(-38f, 0, 0) : Quaternion.identity;
            GameObject cage = Prims.Shape(PrimitiveType.Cylinder, head, "Cage", Vector3.zero, new Vector3(0.45f, 0.025f, 0.45f), Mats.Lit(new Color(0.75f, 0.75f, 0.78f), 0.3f, 0.4f));
            cage.transform.localRotation = Quaternion.Euler(90, 0, 0);
            Material bm = Mats.Lit(new Color(0.55f, 0.65f, 0.75f), 0.2f);
            Prims.Box(head, "BladeA", new Vector3(0, 0, 0.02f), new Vector3(0.36f, 0.07f, 0.015f), bm);
            Prims.Box(head, "BladeB", new Vector3(0, 0, 0.02f), new Vector3(0.07f, 0.36f, 0.015f), bm);
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

        private static void Desk(Transform r, float w, float d, bool c)
        {
            Prims.Box(r, "Top", new Vector3(0, 0.74f, 0), new Vector3(w, 0.05f, d), Mats.Lit(new Color(0.6f, 0.5f, 0.38f), 0.15f), c);
            foreach (Vector2 p in new[] { new Vector2(-w / 2 + 0.05f, -d / 2 + 0.05f), new Vector2(w / 2 - 0.05f, -d / 2 + 0.05f), new Vector2(-w / 2 + 0.05f, d / 2 - 0.05f), new Vector2(w / 2 - 0.05f, d / 2 - 0.05f) })
                Prims.Box(r, "Leg", new Vector3(p.x, 0.36f, p.y), new Vector3(0.05f, 0.72f, 0.05f), Mats.Lit(new Color(0.3f, 0.3f, 0.32f), 0.3f, 0.5f));
            Prims.Box(r, "Laptop", new Vector3(0, 0.78f, 0.05f), new Vector3(0.34f, 0.02f, 0.24f), Mats.Lit(new Color(0.2f, 0.2f, 0.23f), 0.4f, 0.4f));
            Prims.Box(r, "Screen", new Vector3(0, 0.92f, 0.17f), new Vector3(0.34f, 0.24f, 0.015f), Mats.Glow(new Color(0.1f, 0.1f, 0.12f), new Color(0.3f, 0.8f, 1.5f), false));
        }

        private static void Sofa(Transform r, float w, float d, bool c, Color color)
        {
            Material m = Mats.Lit(color, 0.1f);
            Material dark = Mats.Lit(color * 0.82f, 0.1f);
            Prims.Box(r, "Seat", new Vector3(0, 0.25f, 0), new Vector3(w, 0.4f, d), m, c);
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
