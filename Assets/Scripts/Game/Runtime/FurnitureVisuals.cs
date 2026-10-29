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

        public static GameObject Build(Transform parent, FurnitureDef def, bool collider)
        {
            var root = new GameObject("F_" + def.Id).transform;
            root.SetParent(parent, false);
            float w = (float)def.Width, d = (float)def.Depth;
            bool c = collider;
            switch (def.Id)
            {
                case FurnitureCatalog.BedBoard: Bed(root, w, d, c, 0); break;
                case "bed_spring": Bed(root, w, d, c, 1); break;
                case "bed_good": Bed(root, w, d, c, 2); break;
                case FurnitureCatalog.WardrobeCloth: Wardrobe(root, w, d, c, false); break;
                case "wardrobe_wood": Wardrobe(root, w, d, c, true); break;
                case FurnitureCatalog.TableCooker: TableCooker(root, w, d, c); break;
                case "stool":
                    Prims.Shape(PrimitiveType.Cylinder, root, "Stool", new Vector3(0, 0.2f, 0), new Vector3(0.34f, 0.2f, 0.34f), Mats.Lit(new Color(0.7f, 0.25f, 0.2f), 0.3f), c);
                    break;
                case "fan_broken": Fan(root, c, true); break;
                case "fan_new": Fan(root, c, false); break;
                case "junk": Junk(root, c); break;
                case "aircon": AirCon(root); break;
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
            Prims.Box(r, "CookerLight", new Vector3(-0.35f, 0.9f, -0.17f), new Vector3(0.05f, 0.03f, 0.02f), Mats.Glow(new Color(0.6f, 0.1f, 0.1f), new Color(3f, 0.3f, 0.2f), false));
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
        private string _signature = "";

        /// <summary>地面家具的占地（走路用，x/y = 房间局部 x/z）。</summary>
        public readonly List<Rect> Blockers = new List<Rect>();

        public RoomFurniture(Transform parent)
        {
            _parent = parent;
        }

        /// <summary>布局有变化就重建，返回是否重建了。</summary>
        public bool Sync(IReadOnlyList<PlacedFurniture> layout, RoomSpec room)
        {
            var sb = new System.Text.StringBuilder();
            foreach (PlacedFurniture p in layout) sb.Append(p.Id).Append(':').Append(p.X).Append(':').Append(p.Z).Append(':').Append(p.Rot).Append(';');
            string sig = sb.ToString();
            if (sig == _signature) return false;
            _signature = sig;

            foreach (GameObject go in _items) Kill.It(go);
            _items.Clear();
            Blockers.Clear();
            for (int i = 0; i < layout.Count; i++)
            {
                FurnitureDef def = FurnitureCatalog.Get(layout[i].Id);
                if (def == null) continue;
                GameObject go = FurnitureVisuals.Build(_parent, def, true);
                FurnitureVisuals.Place(go, def, layout[i], room);
                var pv = go.AddComponent<PlacedVisual>();
                pv.Index = i;
                pv.FurnitureId = def.Id;
                _items.Add(go);
                if (def.Slot == FurnitureSlot.Floor)
                {
                    RectD f = RoomPlanner.Footprint(room, def, layout[i]);
                    Blockers.Add(new Rect((float)f.MinX, (float)f.MinZ, (float)(f.MaxX - f.MinX), (float)(f.MaxZ - f.MinZ)));
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
            _signature = "";
        }
    }
}
