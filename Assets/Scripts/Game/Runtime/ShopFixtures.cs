using System.Collections.Generic;
using PCShopSell.Core.Catalog;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    /// <summary>一个货架上摆的同类配件。库存多少决定亮出几件。</summary>
    internal sealed class ShelfDisplay
    {
        public PartCategory Category;
        /// <summary>货架在店内的局部位置（店内根节点坐标）。</summary>
        public Vector3 Pos;
        /// <summary>最上层摆的整盒。</summary>
        public readonly List<GameObject> BoxedItems = new List<GameObject>();

        /// <summary>下面两层摆的散件（先摆下一层再摆上一层）。</summary>
        public readonly List<GameObject> LooseItems = new List<GameObject>();
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
                case PartCategory.Gpu: return new Color(0.85f, 0.25f, 0.25f);
                case PartCategory.Cpu: return new Color(0.2f, 0.45f, 0.9f);
                case PartCategory.Memory: return new Color(0.2f, 0.75f, 0.4f);
                case PartCategory.Storage: return new Color(0.95f, 0.65f, 0.15f);
                case PartCategory.Motherboard: return new Color(0.6f, 0.35f, 0.85f);
                default: return new Color(0.2f, 0.8f, 0.85f);
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

            Material wood = Mats.Lit(new Color(0.45f, 0.35f, 0.25f));
            Material frame = Mats.Lit(new Color(0.3f, 0.32f, 0.36f), 0.5f);
            Prims.Box(root, "Back", new Vector3(0, 0.9f, 0.175f), new Vector3(1.4f, 1.8f, 0.05f), wood);
            Prims.Box(root, "PostL", new Vector3(-0.7f, 0.9f, 0f), new Vector3(0.05f, 1.8f, 0.36f), frame);
            Prims.Box(root, "PostR", new Vector3(0.7f, 0.9f, 0f), new Vector3(0.05f, 1.8f, 0.36f), frame);
            foreach (float y in BoardCenters) Prims.Box(root, "Board", new Vector3(0, y, 0f), new Vector3(1.4f, 0.04f, 0.36f), wood);
            Color cc = CategoryColor(cat);
            Prims.Box(root, "Sign", new Vector3(0, 1.93f, -0.1f), new Vector3(1.0f, 0.14f, 0.04f), Mats.Glow(cc * 0.6f, cc * 1.6f, false));

            // 板边的标记：整盒层纸箱色，散件层蓝灰色
            Material boxedTag = Mats.Lit(new Color(0.8f, 0.62f, 0.38f));
            Material looseTag = Mats.Lit(new Color(0.36f, 0.5f, 0.68f));
            for (int r = 0; r < Rows; r++)
                Prims.Box(root, "Tag", new Vector3(0, BoardCenters[r] - 0.002f, -0.185f), new Vector3(1.38f, 0.035f, 0.012f), r == Rows - 1 ? boxedTag : looseTag);

            var display = new ShelfDisplay { Category = cat, Pos = pos };
            // 最上层：整盒
            AddRow(root, cat, true, BoardCenters[Rows - 1] + 0.02f, display.BoxedItems);
            // 下面两层：散件
            AddRow(root, cat, false, BoardCenters[0] + 0.02f, display.LooseItems);
            AddRow(root, cat, false, BoardCenters[1] + 0.02f, display.LooseItems);
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
        /// <summary>整机展示台：一张长桌，两套开着机的整机（显示器 + 主机 + 键盘）。正面朝 -z，台下铺地毯。</summary>
        public static void BuildPcDesk(Transform parent, Vector3 pos, int seed)
        {
            var root = new GameObject("PcDesk").transform;
            root.SetParent(parent, false);
            root.localPosition = pos;

            Prims.Box(root, "Rug", new Vector3(0, 0.012f, -0.3f), new Vector3(3.2f, 0.014f, 2.4f), Mats.Lit(new Color(0.22f, 0.3f, 0.5f)));
            Prims.Box(root, "Desk", new Vector3(0, 0.4f, 0), new Vector3(2.4f, 0.8f, 0.9f), Mats.Lit(new Color(0.5f, 0.4f, 0.3f)));
            Prims.Box(root, "DeskTop", new Vector3(0, 0.805f, 0), new Vector3(2.44f, 0.02f, 0.94f), Mats.Lit(new Color(0.75f, 0.75f, 0.78f), 0.5f));

            Color[] screens = { new Color(0.3f, 1.0f, 1.8f), new Color(1.2f, 0.4f, 1.6f), new Color(0.4f, 1.5f, 0.6f), new Color(1.6f, 0.9f, 0.3f) };
            for (int i = 0; i < 2; i++)
            {
                float cx = i == 0 ? -0.6f : 0.6f;
                Color sc = screens[(seed + i) % screens.Length];
                Prims.Box(root, "Stand", new Vector3(cx - 0.1f, 0.88f, 0.12f), new Vector3(0.08f, 0.14f, 0.08f), Mats.Lit(new Color(0.12f, 0.12f, 0.14f)));
                Prims.Box(root, "Screen", new Vector3(cx - 0.1f, 1.12f, 0.12f), new Vector3(0.62f, 0.38f, 0.03f), Mats.Glow(sc * 0.2f, sc, false));
                Prims.Box(root, "Keyboard", new Vector3(cx - 0.1f, 0.82f, -0.2f), new Vector3(0.4f, 0.02f, 0.13f), Mats.Lit(new Color(0.85f, 0.85f, 0.88f)));
                Prims.Box(root, "Tower", new Vector3(cx + 0.32f, 1.06f, 0.1f), new Vector3(0.26f, 0.5f, 0.4f), Mats.Lit(new Color(0.1f, 0.1f, 0.12f), 0.5f));
                Prims.Box(root, "TowerGlass", new Vector3(cx + 0.32f, 1.08f, -0.102f), new Vector3(0.19f, 0.38f, 0.004f), Mats.Glow(sc * 0.15f, sc * 0.8f, false));
            }
            // 价签牌
            Prims.Box(root, "Tag", new Vector3(0, 0.84f, -0.42f), new Vector3(0.5f, 0.05f, 0.02f), Mats.Glow(new Color(0.9f, 0.8f, 0.2f), new Color(2.2f, 1.8f, 0.3f), false));
        }
    }
}
