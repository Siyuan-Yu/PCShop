using System.Collections.Generic;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Home;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    /// <summary>一个货架上摆的同类配件。库存多少决定亮出几件。</summary>
    internal sealed class ShelfDisplay
    {
        public PartCategory Category;

        /// <summary>摆的是杂货（烟酒饮料之类），不对应哪类配件。</summary>
        public bool Goods;
        public bool IsDisplay;
        public int MaxItems=int.MaxValue;

        /// <summary>货架还没设置摆什么：空架子。</summary>
        public bool Unset;

        /// <summary>设置的货类（ShelfKinds 的 id），没设置是 null。</summary>
        public string KindId;

        /// <summary>这个货架在店面布局里的下标。</summary>
        public int LayoutIndex;

        /// <summary>杂货架上的货（按杂货总件数一件件亮出来）。</summary>
        public readonly List<GameObject> GoodsItems = new List<GameObject>();

        /// <summary>货架在店内的局部位置（店内根节点坐标）。</summary>
        public Vector3 Pos;
        /// <summary>最上层摆的整盒。</summary>
        public readonly List<GameObject> BoxedItems = new List<GameObject>();

        /// <summary>下面两层摆的散件（先摆下一层再摆上一层）。</summary>
        public readonly List<GameObject> LooseItems = new List<GameObject>();

        /// <summary>整个货架一件货都没有时亮起的红色“缺货”条。</summary>
        public GameObject EmptyMark;
    }

    /// <summary>店内陈设：分品类的货架（显卡、CPU、内存、硬盘、主板、机箱电源）和整机展示台。全是积木拼的低模。</summary>
    internal static class ShopFixtures
    {
        public const int Columns = 4;
        public const int Rows = 3;
        private static readonly float[] BoardCenters = { 0.03f, 0.63f, 1.23f, 1.81f };

        public static Color CategoryColor(PartCategory c)
        {
            switch (c)
            {
                case PartCategory.Gpu: return ArtDirection.Clay;
                case PartCategory.Cpu: return ArtDirection.Blue;
                case PartCategory.Memory: return ArtDirection.Sage;
                case PartCategory.Storage: return ArtDirection.Brass;
                case PartCategory.Motherboard: return ArtDirection.Hex("958593");
                default: return ArtDirection.Hex("63988F");
            }
        }

        /// <summary>
        /// 货架。局部坐标：x 为宽（1.4 m），z 朝店内为负，靠墙的一面在 +z。
        /// 最上面一层摆整盒（板边是纸箱色），下面两层摆散件（板边是蓝灰色），和货架窗口里的上下层一致。
        /// </summary>
        public static ShelfDisplay BuildShelf(Transform parent, Vector3 pos, float yawDeg, PartCategory cat)
        {
            var root = new GameObject("Shelf_" + cat).transform;
            root.SetParent(parent, false);
            root.localPosition = pos;
            root.localRotation = Quaternion.Euler(0, yawDeg, 0);

            Material wood = Mats.Lit(ArtDirection.Wood);
            Material frame = Mats.Lit(ArtDirection.Ink, 0.5f);
            Prims.Box(root, "Back", new Vector3(0, 0.9f, 0.175f), new Vector3(1.4f, 1.8f, 0.05f), wood);
            Prims.Box(root, "PostL", new Vector3(-0.7f, 0.9f, 0f), new Vector3(0.05f, 1.8f, 0.36f), frame);
            Prims.Box(root, "PostR", new Vector3(0.7f, 0.9f, 0f), new Vector3(0.05f, 1.8f, 0.36f), frame);
            foreach (float y in BoardCenters) Prims.Box(root, "Board", new Vector3(0, y, 0f), new Vector3(1.4f, 0.04f, 0.36f), wood);
            Color cc = CategoryColor(cat);
            Prims.Box(root, "Sign", new Vector3(0, 1.93f, -0.1f), new Vector3(1.0f, 0.14f, 0.04f), Mats.Lit(cc));

            Prims.SignText(root, PartCatalog.CategoryName(cat), new Vector3(0, 1.93f, -0.125f), 0.9f, 0.13f, ArtDirection.Cream);
            // 板边的标记：整盒层纸箱色，散件层蓝灰色
            Material boxedTag = Mats.Lit(ArtDirection.Brass);
            Material looseTag = Mats.Lit(ArtDirection.Blue);
            for (int r = 0; r < Rows; r++)
                Prims.Box(root, "Tag", new Vector3(0, BoardCenters[r] - 0.002f, -0.185f), new Vector3(1.38f, 0.035f, 0.012f), r == Rows - 1 ? boxedTag : looseTag);

            var display = new ShelfDisplay { Category = cat, Pos = pos };
            // 最上层：整盒
            AddRow(root, cat, true, BoardCenters[Rows - 1] + 0.02f, display.BoxedItems);
            // 下面两层：散件
            AddRow(root, cat, false, BoardCenters[0] + 0.02f, display.LooseItems);
            AddRow(root, cat, false, BoardCenters[1] + 0.02f, display.LooseItems);
            display.EmptyMark = Prims.Box(root, "EmptyMark", new Vector3(0, 0.98f, -0.2f), new Vector3(1.1f, 0.2f, 0.02f), Mats.Glow(new Color(0.8f, 0.15f, 0.1f), new Color(3f, 0.4f, 0.25f), false));
            display.EmptyMark.SetActive(false);
            return display;
        }

        /// <summary>某种配件在货架上占多宽（米，含间隙），决定一层能摆几件。</summary>
        private static float ItemWidth(PartCategory cat, bool boxed)
        {
            switch (cat)
            {
                case PartCategory.Gpu: return boxed ? 0.40f : 0.32f;
                case PartCategory.Cpu: return boxed ? 0.14f : 0.07f;
                case PartCategory.Memory: return boxed ? 0.17f : 0.16f;
                case PartCategory.Storage: return boxed ? 0.16f : 0.12f;
                case PartCategory.PowerCase: return boxed ? 0.27f : 0.24f;
                default: return boxed ? 0.36f : 0.32f;
            }
        }

        private static void AddRow(Transform root, PartCategory cat, bool boxed, float y, List<GameObject> into)
        {
            float w = ItemWidth(cat, boxed);
            int cols = Mathf.Clamp(Mathf.FloorToInt(1.28f / w), 1, 6);
            float x0 = -(cols - 1) * w / 2f;
            for (int c = 0; c < cols; c++)
                into.Add(PartModel(root, cat, new Vector3(x0 + c * w, y, -0.01f), boxed));
        }

        private static Material BoxMat(PartCategory cat) => Mats.Lit(CategoryColor(cat) * 0.9f);

        /// <summary>
        /// 杂货架：和配件货架同款木架，三层都摆杂货（烟盒、酒瓶、饮料瓶、点心盒、水果），亮出几件看杂货一共有多少。
        /// </summary>
        public static ShelfDisplay BuildGoodsShelf(Transform parent, string kindId)
        {
            var root = new GameObject("Shelf_" + kindId).transform;
            root.SetParent(parent, false);

            Material wood = Mats.Lit(ArtDirection.Wood);
            Material frame = Mats.Lit(ArtDirection.Ink, 0.5f);
            Prims.Box(root, "Back", new Vector3(0, 0.9f, 0.175f), new Vector3(1.4f, 1.8f, 0.05f), wood);
            Prims.Box(root, "PostL", new Vector3(-0.7f, 0.9f, 0f), new Vector3(0.05f, 1.8f, 0.36f), frame);
            Prims.Box(root, "PostR", new Vector3(0.7f, 0.9f, 0f), new Vector3(0.05f, 1.8f, 0.36f), frame);
            foreach (float y in BoardCenters) Prims.Box(root, "Board", new Vector3(0, y, 0f), new Vector3(1.4f, 0.04f, 0.36f), wood);
            int model = GoodsModelOf(kindId);
            Color cc = GoodsColor(kindId);
            Prims.Box(root, "Sign", new Vector3(0, 1.93f, -0.1f), new Vector3(1.0f, 0.14f, 0.04f), Mats.Lit(cc));
            if(kindId!=null&&kindId.StartsWith("clothing_"))Prims.SignText(root,Core.Inventory.ShelfKinds.NameOf(kindId),new Vector3(0,1.93f,-.125f),.94f,.12f,ArtDirection.Cream);

            var display = new ShelfDisplay { Goods = true, Category = PartCategory.Gpu, KindId = kindId };
            int perRow = model==11?4:7;
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < perRow; c++)
                {
                    float x = (c - (perRow - 1) / 2f) * (model==11?.31f:.18f);
                    display.GoodsItems.Add(GoodsModel(root, model, new Vector3(x, BoardCenters[r] + 0.02f, -0.01f)));
                }
            display.EmptyMark = Prims.Box(root, "EmptyMark", new Vector3(0, 0.98f, -0.2f), new Vector3(1.1f, 0.2f, 0.02f), Mats.Glow(new Color(0.8f, 0.15f, 0.1f), new Color(3f, 0.4f, 0.25f), false));
            display.EmptyMark.SetActive(false);
            return display;
        }

        private static int GoodsModelOf(string kindId)
        {
            if(kindId!=null&&kindId.StartsWith("clothing_"))return 11;
            switch (kindId)
            {
                case "smoke": return 0;
                case "secondhand": return 12;
                case "liquor": return 1;
                case "drink": return 2;
                case "snack": return 3;
                case "flowers": return 5;
                case "meals": return 6;
                case "model_harbor": return 7;
                case "model_rainbot": return 8;
                case "model_garden": return 9;
                case "model_starlight": return 10;
                default: return 4;
            }
        }

        public static Color GoodsColor(string kindId)
        {
            switch (kindId)
            {
                case "smoke": return new Color(0.9f, 0.3f, 0.3f);
                case "liquor": return new Color(0.3f, 0.75f, 0.45f);
                case "drink": return new Color(0.35f, 0.7f, 0.95f);
                case "flowers": return new Color(0.9f, 0.45f, 0.65f);
                case "produce": return new Color(0.4f, 0.7f, 0.35f);
                case "meals": return new Color(0.9f, 0.65f, 0.3f);
                case "snack": return new Color(0.95f, 0.65f, 0.25f);
                case "model_harbor": return new Color(.35f, .7f, .75f);
                case "model_rainbot": return new Color(.6f, .65f, .8f);
                case "model_garden": return new Color(.5f, .75f, .45f);
                case "model_starlight": return new Color(.9f, .7f, .35f);
                default: return new Color(0.95f, 0.8f, 0.3f);
            }
        }

        /// <summary>还没设置摆什么的货架：光架子，招牌是灰的。</summary>
        public static ShelfDisplay BuildEmptyShelf(Transform parent)
        {
            var root = new GameObject("Shelf_Unset").transform;
            root.SetParent(parent, false);
            Material wood = Mats.Lit(ArtDirection.Wood);
            Material frame = Mats.Lit(ArtDirection.Ink, 0.5f);
            Prims.Box(root, "Back", new Vector3(0, 0.9f, 0.175f), new Vector3(1.4f, 1.8f, 0.05f), wood);
            Prims.Box(root, "PostL", new Vector3(-0.7f, 0.9f, 0f), new Vector3(0.05f, 1.8f, 0.36f), frame);
            Prims.Box(root, "PostR", new Vector3(0.7f, 0.9f, 0f), new Vector3(0.05f, 1.8f, 0.36f), frame);
            foreach (float y in BoardCenters) Prims.Box(root, "Board", new Vector3(0, y, 0f), new Vector3(1.4f, 0.04f, 0.36f), wood);
            Prims.Box(root, "Sign", new Vector3(0, 1.93f, -0.1f), new Vector3(1.0f, 0.14f, 0.04f), Mats.Lit(new Color(0.45f, 0.45f, 0.48f)));
            return new ShelfDisplay { Unset = true, Category = PartCategory.Gpu };
        }

        /// <summary>按设置的货类造货架：没设置是空架子，配件类摆那类配件，杂货类摆对应的杂货。</summary>
        public static ShelfDisplay BuildShelfFor(Transform parent, string kindId)
        {
            Core.Inventory.ShelfKind k = Core.Inventory.ShelfKinds.Get(kindId);
            if (k == null) return BuildEmptyShelf(parent);
            if (k.IsGoods) return BuildGoodsShelf(parent, k.Id);
            ShelfDisplay d = BuildShelf(parent, Vector3.zero, 0f, k.Category);
            d.KindId = k.Id;
            return d;
        }

        /// <summary>一件杂货的小模型：0 烟盒、1 酒瓶、2 饮料瓶、3 点心盒、4 水果。</summary>
        private static GameObject GoodsModel(Transform parent, int kind, Vector3 pos)
        {
            var g = new GameObject("Goods" + kind);
            Transform t = g.transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            switch (kind)
            {
                case 12:
                    for(int product=0;product<4;product++)
                    {
                        var old=new GameObject("UsedProduct_"+product);old.transform.SetParent(t,false);
                        var p=old.transform;var metal=Mats.Lit(ArtDirection.Brass);var dark=Mats.Lit(ArtDirection.Ink);
                        if(product==0)
                        {
                            Prims.Box(p,"LampBase",new Vector3(0,.015f,0),new Vector3(.12f,.03f,.1f),dark);
                            Prims.Box(p,"LampStem",new Vector3(0,.12f,0),new Vector3(.018f,.21f,.018f),metal);
                            Prims.Box(p,"LampShade",new Vector3(0,.24f,0),new Vector3(.15f,.09f,.12f),Mats.Lit(ArtDirection.Cream));
                        }
                        else if(product==1)
                        {
                            Prims.Box(p,"Speaker",new Vector3(0,.14f,0),new Vector3(.14f,.28f,.13f),dark);
                            Prims.Shape(PrimitiveType.Sphere,p,"Woofer",new Vector3(0,.1f,-.065f),new Vector3(.1f,.1f,.012f),Mats.Lit(Color.gray));
                            Prims.Shape(PrimitiveType.Sphere,p,"Tweeter",new Vector3(0,.22f,-.065f),new Vector3(.05f,.05f,.012f),metal);
                        }
                        else if(product==2)
                        {
                            Prims.Box(p,"WatchStrap",new Vector3(0,.025f,0),new Vector3(.05f,.04f,.19f),dark);
                            Prims.Box(p,"WatchFace",new Vector3(0,.05f,0),new Vector3(.09f,.03f,.09f),metal);
                            Prims.Box(p,"Dial",new Vector3(0,.068f,0),new Vector3(.065f,.008f,.065f),Mats.Lit(ArtDirection.Cream));
                        }
                        else
                        {
                            Prims.Box(p,"OrnamentBase",new Vector3(0,.025f,0),new Vector3(.14f,.05f,.11f),Mats.Lit(ArtDirection.Wood));
                            Prims.Shape(PrimitiveType.Sphere,p,"Ornament",new Vector3(0,.14f,0),new Vector3(.11f,.2f,.09f),metal);
                        }
                        old.SetActive(product==0);
                    }
                    break;
                case 11:
                    Color cloth=((int)Mathf.Round(pos.x*10)&1)==0?ArtDirection.Clay:ArtDirection.Sage;
                    Prims.Box(t,"FoldedTShirt",new Vector3(0,.055f,-.025f),new Vector3(.23f,.07f,.23f),Mats.Lit(cloth));
                    Prims.Box(t,"FoldedSleeveL",new Vector3(-.117f,.075f,.045f),new Vector3(.05f,.065f,.13f),Mats.Lit(cloth*.85f));
                    Prims.Box(t,"FoldedSleeveR",new Vector3(.117f,.075f,.045f),new Vector3(.05f,.065f,.13f),Mats.Lit(cloth*.85f));
                    Prims.Box(t,"VisibleNeckline",new Vector3(0,.095f,.055f),new Vector3(.07f,.025f,.065f),Mats.Lit(ArtDirection.Cream));
                    break;
                case 7:
                    Prims.Box(t, "ToyHull", new Vector3(0, .045f, 0), new Vector3(.15f, .07f, .1f), Mats.Lit(new Color(.3f, .65f, .7f)));
                    Prims.Box(t, "ToyMast", new Vector3(0, .16f, 0), new Vector3(.012f, .2f, .012f), Mats.Lit(ArtDirection.Wood));
                    Prims.Box(t, "ToySail", new Vector3(.035f, .19f, 0), new Vector3(.065f, .13f, .012f), Mats.Lit(ArtDirection.Cream));
                    break;
                case 8:
                    Prims.Box(t, "ToyRobotBody", new Vector3(0, .11f, 0), new Vector3(.09f, .13f, .07f), Mats.Lit(new Color(.48f, .55f, .72f)));
                    Prims.Box(t, "ToyRobotHead", new Vector3(0, .21f, 0), new Vector3(.12f, .09f, .09f), Mats.Lit(ArtDirection.Cream));
                    Prims.Box(t, "ToyRobotEye", new Vector3(0, .215f, -.046f), new Vector3(.08f, .025f, .006f), Mats.Lit(ArtDirection.Ink));
                    Prims.Box(t, "ToyRobotFeet", new Vector3(0, .025f, 0), new Vector3(.14f, .05f, .09f), Mats.Lit(ArtDirection.Ink));
                    break;
                case 9:
                    Prims.Shape(PrimitiveType.Sphere, t, "ToyGardenBody", new Vector3(0, .09f, 0), new Vector3(.12f, .16f, .1f), Mats.Lit(new Color(.65f, .78f, .45f)));
                    Prims.Shape(PrimitiveType.Sphere, t, "ToyLeafLeft", new Vector3(-.035f, .19f, 0), new Vector3(.07f, .025f, .045f), Mats.Lit(new Color(.25f, .6f, .3f)));
                    Prims.Shape(PrimitiveType.Sphere, t, "ToyLeafRight", new Vector3(.035f, .19f, 0), new Vector3(.07f, .025f, .045f), Mats.Lit(new Color(.35f, .7f, .35f)));
                    break;
                case 10:
                    Prims.Box(t, "ToyRover", new Vector3(0, .07f, 0), new Vector3(.13f, .09f, .1f), Mats.Lit(new Color(.87f, .66f, .3f)));
                    Prims.Shape(PrimitiveType.Sphere, t, "ToyObservatory", new Vector3(0, .16f, 0), Vector3.one * .1f, Mats.Lit(ArtDirection.Cream));
                    Prims.Box(t, "ToyWheels", new Vector3(0, .025f, 0), new Vector3(.16f, .045f, .12f), Mats.Lit(ArtDirection.Ink));
                    break;
                case 5:
                    Prims.Shape(PrimitiveType.Cylinder, t, "FlowerPot", new Vector3(0, 0.05f, 0), new Vector3(0.09f, 0.05f, 0.09f), Mats.Lit(ArtDirection.Wood));
                    Prims.Box(t, "Stem", new Vector3(0, 0.17f, 0), new Vector3(0.014f, 0.2f, 0.014f), Mats.Lit(new Color(0.2f, 0.55f, 0.2f)));
                    for (int petal = 0; petal < 5; petal++)
                    {
                        float a = petal * Mathf.PI * 2 / 5;
                        Prims.Shape(PrimitiveType.Sphere, t, "Petal", new Vector3(Mathf.Cos(a) * 0.045f, 0.27f, Mathf.Sin(a) * 0.045f), Vector3.one * 0.065f, Mats.Lit(new Color(0.92f, 0.3f, 0.48f)));
                    }
                    break;
                case 6:
                    Prims.Box(t, "MealTray", new Vector3(0, 0.035f, 0), new Vector3(0.18f, 0.07f, 0.13f), Mats.Lit(ArtDirection.Cream));
                    Prims.Box(t, "Food", new Vector3(0, 0.08f, 0), new Vector3(0.14f, 0.03f, 0.09f), Mats.Lit(new Color(0.85f, 0.58f, 0.25f)));
                    break;
                case 0:
                    Prims.Box(t, "Pack", new Vector3(0, 0.07f, 0), new Vector3(0.12f, 0.14f, 0.05f), Mats.Lit(new Color(0.8f, 0.15f, 0.15f)));
                    Prims.Box(t, "Band", new Vector3(0, 0.09f, -0.026f), new Vector3(0.12f, 0.03f, 0.004f), Mats.Lit(new Color(0.95f, 0.8f, 0.3f)));
                    break;
                case 1:
                    Prims.Shape(PrimitiveType.Cylinder, t, "Bottle", new Vector3(0, 0.14f, 0), new Vector3(0.07f, 0.14f, 0.07f), Mats.Lit(new Color(0.15f, 0.4f, 0.25f), 0.6f));
                    Prims.Shape(PrimitiveType.Cylinder, t, "Neck", new Vector3(0, 0.33f, 0), new Vector3(0.03f, 0.05f, 0.03f), Mats.Lit(new Color(0.15f, 0.4f, 0.25f), 0.6f));
                    Prims.Box(t, "Label", new Vector3(0, 0.12f, -0.036f), new Vector3(0.06f, 0.09f, 0.004f), Mats.Lit(new Color(0.95f, 0.93f, 0.86f)));
                    break;
                case 2:
                    Prims.Shape(PrimitiveType.Cylinder, t, "Bottle", new Vector3(0, 0.11f, 0), new Vector3(0.06f, 0.11f, 0.06f), Mats.Lit(new Color(0.3f, 0.65f, 0.9f), 0.6f));
                    Prims.Shape(PrimitiveType.Cylinder, t, "Cap", new Vector3(0, 0.23f, 0), new Vector3(0.03f, 0.015f, 0.03f), Mats.Lit(new Color(0.9f, 0.9f, 0.92f)));
                    break;
                case 3:
                    Prims.Box(t, "Box", new Vector3(0, 0.04f, 0), new Vector3(0.14f, 0.08f, 0.1f), Mats.Lit(new Color(0.9f, 0.62f, 0.25f)));
                    Prims.Box(t, "Ribbon", new Vector3(0, 0.04f, -0.052f), new Vector3(0.02f, 0.08f, 0.004f), Mats.Lit(new Color(0.85f, 0.2f, 0.25f)));
                    break;
                default:
                    Prims.Box(t, "Crate", new Vector3(0, 0.05f, 0), new Vector3(0.15f, 0.1f, 0.11f), Mats.Lit(new Color(0.6f, 0.45f, 0.28f)));
                    Prims.Shape(PrimitiveType.Sphere, t, "Fruit", new Vector3(-0.03f, 0.115f, 0), Vector3.one * 0.07f, Mats.Lit(new Color(0.9f, 0.3f, 0.2f)));
                    Prims.Shape(PrimitiveType.Sphere, t, "Fruit2", new Vector3(0.035f, 0.115f, 0), Vector3.one * 0.07f, Mats.Lit(ArtDirection.Brass));
                    break;
            }
            return g;
        }

        /// <summary>
        /// 一件配件的小模型，脚底在 pos，正面朝 -z。尺寸按真实配件：
        /// 显卡约 30×13 cm、CPU 约 4.5 cm、内存条约 13×3 cm、2.5 寸硬盘 10×7 cm、ATX 主板约 30×24 cm；整盒是零售包装盒。
        /// </summary>
        public static GameObject PartModel(Transform parent, PartCategory cat, Vector3 pos, bool boxed = false)
        {
            var g = new GameObject((boxed ? "Box_" : "Part_") + cat);
            Transform t = g.transform;
            t.SetParent(parent, false);
            t.localPosition = pos;

            Material dark = Mats.Lit(new Color(0.12f, 0.12f, 0.14f), 0.4f);
            Material silver = Mats.Lit(new Color(0.8f, 0.82f, 0.85f), 0.6f);
            Material gold = Mats.Lit(new Color(0.9f, 0.7f, 0.2f), 0.7f);
            if (boxed)
            {
                Vector3 dim;
                switch (cat)
                {
                    case PartCategory.Gpu: dim = new Vector3(0.38f, 0.17f, 0.08f); break;
                    case PartCategory.Cpu: dim = new Vector3(0.12f, 0.13f, 0.07f); break;
                    case PartCategory.Memory: dim = new Vector3(0.15f, 0.08f, 0.02f); break;
                    case PartCategory.Storage: dim = new Vector3(0.14f, 0.10f, 0.04f); break;
                    case PartCategory.PowerCase: dim = new Vector3(0.24f, 0.48f, 0.30f); break;
                    default: dim = new Vector3(0.34f, 0.28f, 0.07f); break;
                }
                float fz = -dim.z / 2f - 0.002f;
                Prims.Box(t, "Carton", new Vector3(0, dim.y / 2f, 0), dim, Mats.Lit(new Color(0.78f, 0.62f, 0.4f)));
                Prims.Box(t, "Band", new Vector3(0, dim.y * 0.09f, fz), new Vector3(dim.x, dim.y * 0.18f, 0.004f), BoxMat(cat));
                Prims.Box(t, "Label", new Vector3(0, dim.y * 0.56f, fz), new Vector3(dim.x * 0.72f, dim.y * 0.56f, 0.004f), Mats.Lit(new Color(0.95f, 0.93f, 0.86f)));
                Prims.Box(t, "Art", new Vector3(0, dim.y * 0.56f, fz - 0.003f), new Vector3(dim.x * 0.4f, dim.y * 0.28f, 0.004f), cat == PartCategory.Cpu ? silver : dark);
                return g;
            }
            switch (cat)
            {
                case PartCategory.Gpu:
                    Prims.Box(t, "Body", new Vector3(0, 0.022f, 0), new Vector3(0.30f, 0.044f, 0.13f), dark);
                    Prims.Shape(PrimitiveType.Cylinder, t, "FanL", new Vector3(-0.075f, 0.046f, 0), new Vector3(0.06f, 0.004f, 0.06f), silver);
                    Prims.Shape(PrimitiveType.Cylinder, t, "FanR", new Vector3(0.075f, 0.046f, 0), new Vector3(0.06f, 0.004f, 0.06f), silver);
                    Prims.Box(t, "Stripe", new Vector3(0, 0.03f, -0.067f), new Vector3(0.30f, 0.02f, 0.006f), Mats.Lit(new Color(0.85f, 0.2f, 0.2f)));
                    Prims.Box(t, "Bracket", new Vector3(-0.152f, 0.06f, 0f), new Vector3(0.006f, 0.12f, 0.11f), silver);
                    break;
                case PartCategory.Cpu:
                    Prims.Box(t, "Substrate", new Vector3(0, 0.002f, 0), new Vector3(0.045f, 0.004f, 0.045f), Mats.Lit(new Color(0.24f, 0.5f, 0.34f)));
                    Prims.Box(t, "Heatspreader", new Vector3(0, 0.0055f, 0), new Vector3(0.032f, 0.003f, 0.032f), silver);
                    break;
                case PartCategory.Memory:
                    for (int s = 0; s < 2; s++)
                    {
                        float z = s == 0 ? -0.03f : 0.03f;
                        Prims.Box(t, "Stick", new Vector3(0, 0.02f, z), new Vector3(0.133f, 0.032f, 0.006f), Mats.Lit(new Color(0.1f, 0.4f, 0.25f)));
                        for (int k = 0; k < 4; k++)
                            Prims.Box(t, "Chip", new Vector3(-0.045f + k * 0.03f, 0.022f, z - 0.005f), new Vector3(0.022f, 0.02f, 0.006f), dark);
                        Prims.Box(t, "Gold", new Vector3(0, 0.002f, z), new Vector3(0.12f, 0.004f, 0.008f), gold);
                    }
                    break;
                case PartCategory.Storage:
                    for (int s = 0; s < 3; s++)
                    {
                        Prims.Box(t, "Ssd", new Vector3(0, 0.005f + s * 0.011f, 0), new Vector3(0.10f, 0.01f, 0.07f), dark);
                        Prims.Box(t, "Label", new Vector3(0, 0.0105f + s * 0.011f, 0), new Vector3(0.07f, 0.002f, 0.045f), Mats.Lit(new Color(0.9f, 0.6f, 0.15f)));
                    }
                    break;
                case PartCategory.Motherboard:
                    Prims.Box(t, "Pcb", new Vector3(0, 0.003f, 0), new Vector3(0.30f, 0.006f, 0.245f), Mats.Lit(new Color(0.5f, 0.18f, 0.24f)));
                    Prims.Box(t, "Socket", new Vector3(-0.03f, 0.008f, 0.02f), new Vector3(0.06f, 0.004f, 0.06f), silver);
                    Prims.Box(t, "Ram1", new Vector3(0.06f, 0.008f, 0.03f), new Vector3(0.008f, 0.004f, 0.13f), dark);
                    Prims.Box(t, "Ram2", new Vector3(0.08f, 0.008f, 0.03f), new Vector3(0.008f, 0.004f, 0.13f), dark);
                    Prims.Box(t, "Pcie", new Vector3(-0.03f, 0.008f, -0.07f), new Vector3(0.16f, 0.004f, 0.012f), dark);
                    break;
                default: // 电源机箱
                    Prims.Box(t, "Case", new Vector3(0, 0.23f, 0), new Vector3(0.21f, 0.46f, 0.34f), dark);
                    Prims.Box(t, "Glass", new Vector3(0, 0.26f, -0.172f), new Vector3(0.16f, 0.34f, 0.004f), Mats.Glow(new Color(0.1f, 0.3f, 0.4f), new Color(0.3f, 1.2f, 1.6f), false));
                    Prims.Box(t, "Psu", new Vector3(0, 0.04f, -0.172f), new Vector3(0.16f, 0.06f, 0.004f), Mats.Lit(new Color(0.3f, 0.3f, 0.34f)));
                    break;
            }
            return g;
        }
        /// <summary>展示台上的一个机位：一套整机（显示器 + 主机 + 键盘），屏幕颜色表示档次。</summary>
        public sealed class PcSlot
        {
            public GameObject Root;
            public Renderer Screen;
            public Renderer Glass;
        }

        private static Material[] _tierScreens;
        private static Material[] _tierGlass;

        /// <summary>入门 / 中端 / 高端整机的屏幕颜色（蓝 / 青 / 紫）。</summary>
        public static readonly Color[] TierColors = { new Color(0.3f, 1.0f, 1.8f), new Color(0.3f, 1.6f, 1.2f), new Color(1.3f, 0.4f, 1.8f) };

        public static void SetSlotTier(PcSlot slot, int tier)
        {
            if (_tierScreens == null)
            {
                _tierScreens = new Material[3];
                _tierGlass = new Material[3];
                for (int i = 0; i < 3; i++)
                {
                    _tierScreens[i] = Mats.Glow(TierColors[i] * 0.2f, TierColors[i], false);
                    _tierGlass[i] = Mats.Glow(TierColors[i] * 0.15f, TierColors[i] * 0.8f, false);
                }
            }
            tier = Mathf.Clamp(tier, 0, 2);
            slot.Screen.sharedMaterial = _tierScreens[tier];
            slot.Glass.sharedMaterial = _tierGlass[tier];
        }

        /// <summary>整机展示台：一张长桌，两个机位。正面朝 -z，台下铺地毯，正面挂“整机现货”灯牌。返回两个机位（没摆机器时隐藏）。</summary>
        public static List<PcSlot> BuildPcDesk(Transform parent, Vector3 pos)
        {
            var root = new GameObject("PcDesk").transform;
            root.SetParent(parent, false);
            root.localPosition = pos;

            Prims.Box(root, "Rug", new Vector3(0, 0.012f, -0.3f), new Vector3(3.2f, 0.014f, 2.4f), Mats.Lit(ArtDirection.Sage));
            Prims.Box(root, "Desk", new Vector3(0, 0.4f, 0), new Vector3(2.4f, 0.8f, 0.9f), Mats.Lit(ArtDirection.Wood));
            Prims.Box(root, "DeskTop", new Vector3(0, 0.805f, 0), new Vector3(2.44f, 0.02f, 0.94f), Mats.Lit(ArtDirection.Cream, 0.5f));
            Prims.Box(root, "Sign", new Vector3(0, 0.55f, -0.46f), new Vector3(1.6f, 0.22f, 0.02f), Mats.Lit(ArtDirection.Ink));

            Prims.SignText(root, "商 品 展 示", new Vector3(0, 0.55f, -0.477f), 1.45f, 0.17f, ArtDirection.Cream);
            var slots = new List<PcSlot>();
            for (int i = 0; i < 2; i++)
            {
                float cx = i == 0 ? -0.6f : 0.6f;
                var g = new GameObject("PcSlot" + i).transform;
                g.SetParent(root, false);
                Prims.Box(g, "Stand", new Vector3(cx - 0.1f, 0.88f, 0.12f), new Vector3(0.08f, 0.14f, 0.08f), Mats.Lit(new Color(0.12f, 0.12f, 0.14f)));
                GameObject screen = Prims.Box(g, "Screen", new Vector3(cx - 0.1f, 1.12f, 0.12f), new Vector3(0.62f, 0.38f, 0.03f), Mats.Lit(Color.black));
                Prims.Box(g, "Keyboard", new Vector3(cx - 0.1f, 0.82f, -0.2f), new Vector3(0.4f, 0.02f, 0.13f), Mats.Lit(new Color(0.85f, 0.85f, 0.88f)));
                Prims.Box(g, "Tower", new Vector3(cx + 0.32f, 1.06f, 0.1f), new Vector3(0.26f, 0.5f, 0.4f), Mats.Lit(new Color(0.1f, 0.1f, 0.12f), 0.5f));
                GameObject glass = Prims.Box(g, "TowerGlass", new Vector3(cx + 0.32f, 1.08f, -0.102f), new Vector3(0.19f, 0.38f, 0.004f), Mats.Lit(Color.black));
                Prims.Box(g, "Tag", new Vector3(cx, 0.84f, -0.42f), new Vector3(0.4f, 0.05f, 0.02f), Mats.Lit(ArtDirection.Cream));
                var slot = new PcSlot { Root = g.gameObject, Screen = screen.GetComponent<Renderer>(), Glass = glass.GetComponent<Renderer>() };
                SetSlotTier(slot, 0);
                slots.Add(slot);
            }
            return slots;
        }

        /// <summary>
        /// 一件店面设施的模型（货架 / 收银台 / 展示桌 / 装机操作台），局部坐标：原点在占地中心的地面上，正面朝 -z，
        /// 摆放时由 <see cref="FurnitureVisuals.Place"/> 按 Rot 转。要拿到的东西（货架陈列、机位、发光屏幕）挂在 <see cref="FixtureHandle"/> 上。
        /// </summary>
        public static void BuildFixture(Transform root, FurnitureDef def, bool collider, string kindId = null)
        {
            var h = root.gameObject.AddComponent<FixtureHandle>();
            h.Kind = def.Fixture;
            float w = (float)def.Width, d = (float)def.Depth;
            float pickH = 1.2f;
            switch (def.Fixture)
            {
                case FixtureKind.Shelf:
                    h.Shelf = BuildShelfFor(root, kindId);
                    if (def.ShelfKg > Core.Inventory.ShelfKinds.CapacityKg)
                    {
                        // 加固（蓝）/ 重型（橙）货架：两边加粗的立柱，一眼能和普通货架分开
                        Color tier = def.Id == Core.Home.FurnitureCatalog.RackHeavy ? ArtDirection.Brass : ArtDirection.Blue;
                        Prims.Box(root, "TierPostL", new Vector3(-0.74f, 0.93f, 0f), new Vector3(0.09f, 1.86f, 0.42f), Mats.Lit(tier, 0.4f));
                        Prims.Box(root, "TierPostR", new Vector3(0.74f, 0.93f, 0f), new Vector3(0.09f, 1.86f, 0.42f), Mats.Lit(tier, 0.4f));
                    }
                    pickH = 2.0f;
                    break;
                case FixtureKind.Counter:
                    Prims.Box(root, "Counter", new Vector3(0, 0.47f, 0), new Vector3(w, 0.94f, d), Mats.Lit(ArtDirection.Sage, 0.15f));
                    Prims.Box(root, "CounterTop", new Vector3(0, 0.97f, 0), new Vector3(w + 0.08f, 0.09f, d + 0.06f), Mats.Lit(ArtDirection.Cream, 0.24f));
                    Prims.Box(root, "CounterPlinth", new Vector3(0, 0.06f, 0), new Vector3(w - 0.08f, 0.12f, d - 0.04f), Mats.Lit(ArtDirection.Ink));
                    for (float x = -w / 2 + 0.12f; x < w / 2; x += 0.18f)
                        Prims.Box(root, "CounterFlute", new Vector3(x, 0.48f, -d / 2 - 0.009f), new Vector3(0.012f, 0.68f, 0.018f), Mats.Lit(ArtDirection.Ink));
                    Prims.Box(root, "CounterBadge", new Vector3(0, 0.64f, -d / 2 - 0.025f), new Vector3(0.42f, 0.15f, 0.025f), Mats.Lit(ArtDirection.Brass));
                    Prims.Box(root, "Monitor", new Vector3(0.2f, 1.25f, 0.05f), new Vector3(0.6f, 0.45f, 0.1f), Mats.Lit(ArtDirection.Cream));
                    Prims.Box(root, "TerminalScreen", new Vector3(0.2f, 1.26f, -0.006f), new Vector3(0.51f, 0.32f, 0.012f), Mats.Glow(ArtDirection.Ink, ArtDirection.Sage * 0.4f, false));
                    for (int line = 0; line < 3; line++) Prims.Box(root, "TerminalText", new Vector3(0.14f, 1.33f - line * 0.06f, -0.015f), new Vector3(0.28f - line * 0.045f, 0.014f, 0.008f), Mats.Lit(ArtDirection.Cream));
                    Prims.Box(root, "Register", new Vector3(-0.5f, 1.1f, -0.05f), new Vector3(0.35f, 0.2f, 0.35f), Mats.Lit(new Color(0.15f, 0.15f, 0.15f)));
                    break;
                case FixtureKind.Display:
                    h.Slots.AddRange(BuildPcDesk(root, Vector3.zero));
                    var displayKind=Core.Inventory.ShelfKinds.Get(kindId);
                    if(displayKind!=null)
                    {
                        foreach(var slot in h.Slots)slot.Root.SetActive(false);
                        h.Slots.Clear();
                        h.Shelf=new ShelfDisplay {KindId=kindId,Goods=displayKind.IsGoods,Category=displayKind.Category,
                            IsDisplay=true,MaxItems=6};
                        for(int i=0;i<6;i++)
                        {
                            var at=new Vector3((i%3-1)*.65f,.83f,(i/3-.5f)*.38f);
                            if(displayKind.IsGoods)
                            {
                                var goods=GoodsModel(root,GoodsModelOf(kindId),at);goods.transform.localScale=Vector3.one*1.6f;
                                h.Shelf.GoodsItems.Add(goods);
                            }
                            else h.Shelf.BoxedItems.Add(PartModel(root,displayKind.Category,at,true));
                        }
                    }
                    pickH = 1.5f;
                    break;
                default: // 装机操作台：有装机单时屏幕亮着
                    Prims.Box(root, "Workbench", new Vector3(0, 0.45f, 0), new Vector3(w, 0.9f, d), Mats.Lit(ArtDirection.Ink, 0.15f));
                    Prims.Box(root, "BenchTop", new Vector3(0, 0.92f, 0), new Vector3(w + 0.04f, 0.06f, d + 0.04f), Mats.Lit(ArtDirection.Wood));
                    Prims.Box(root, "RepairMat", new Vector3(0, 0.958f, -0.05f), new Vector3(w * 0.7f, 0.012f, d * 0.65f), Mats.Lit(ArtDirection.Sage));
                    Prims.Box(root, "BenchCase", new Vector3(-0.3f, 1.15f, 0), new Vector3(0.5f, 0.5f, 0.35f), Mats.Lit(new Color(0.12f, 0.12f, 0.14f), 0.6f));
                    h.Glow.Add(Prims.Box(root, "BenchScreen", new Vector3(0.4f, 1.2f, 0), new Vector3(0.45f, 0.4f, 0.06f), Mats.Glow(new Color(0.1f, 0.1f, 0.12f), new Color(0.2f, 1.4f, 0.6f), false)));
                    Prims.Box(root, "BenchParts", new Vector3(-0.1f, 0.95f, -0.05f), new Vector3(0.3f, 0.08f, 0.3f), Mats.Lit(new Color(0.15f, 0.5f, 0.3f)));
                    break;
            }
            if (collider)
            {
                // 摆放模式里点它就能拾起：整个占地一个盒子碰撞体
                var pick = new GameObject("Pick");
                pick.transform.SetParent(root, false);
                var bc = pick.AddComponent<BoxCollider>();
                bc.center = new Vector3(0, pickH / 2f, 0);
                bc.size = new Vector3(w, pickH, d);
            }
        }
        public static ShelfDisplay BuildClothingRack(Transform parent,string kindId)
        {
            var root=new GameObject("ClothingRail").transform;root.SetParent(parent,false);
            var metal=Mats.Lit(ArtDirection.Ink,.5f);
            foreach(float x in new[]{-.68f,.68f})
            {
                Prims.Box(root,"RailPost",new Vector3(x,.95f,0),new Vector3(.05f,1.9f,.05f),metal);
                Prims.Box(root,"RailFoot",new Vector3(x,.04f,0),new Vector3(.12f,.08f,.52f),metal);
            }
            Prims.Box(root,"RailTop",new Vector3(0,1.87f,0),new Vector3(1.4f,.045f,.05f),metal);
            Prims.SignText(root,Core.Inventory.ShelfKinds.NameOf(kindId),new Vector3(0,2.03f,-.05f),1.2f,.14f,ArtDirection.Ink);
            var display=new ShelfDisplay{Goods=true,KindId=kindId,Unset=Core.Inventory.ShelfKinds.Get(kindId)==null,Category=PartCategory.Gpu};
            for(int i=0;i<6;i++)
            {
                var cloth=new GameObject("HangingGarment").transform;cloth.SetParent(root,false);cloth.localPosition=new Vector3(-.53f+i*.21f,0,0);
                Prims.Box(cloth,"Hanger",new Vector3(0,1.76f,0),new Vector3(.18f,.025f,.04f),Mats.Lit(ArtDirection.Wood));
                Prims.Box(cloth,"Top",new Vector3(0,1.43f,0),new Vector3(.17f,.56f,.07f),Mats.Lit(i%2==0?ArtDirection.Blue:ArtDirection.Clay));
                Prims.Box(cloth,"Sleeve",new Vector3(0,1.57f,0),new Vector3(.22f,.21f,.08f),Mats.Lit(i%2==0?ArtDirection.Blue:ArtDirection.Clay));
                display.GoodsItems.Add(cloth.gameObject);
            }
            display.EmptyMark=Prims.Box(root,"EmptyMark",new Vector3(0,.6f,-.12f),new Vector3(1.0f,.12f,.025f),Mats.Lit(ArtDirection.Clay));
            display.EmptyMark.SetActive(false);return display;
        }
    }

    /// <summary>挂在店面设施模型上，把里面要用到的东西（货架陈列、展示机位、发光屏）交给店内场景。</summary>
    internal sealed class FixtureHandle : MonoBehaviour
    {
        public FixtureKind Kind;
        public ShelfDisplay Shelf;
        public readonly List<ShopFixtures.PcSlot> Slots = new List<ShopFixtures.PcSlot>();
        public readonly List<GameObject> Glow = new List<GameObject>();
    }
}
