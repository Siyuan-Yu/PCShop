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
        public readonly List<GameObject> Items = new List<GameObject>();
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

        /// <summary>货架。局部坐标：x 为宽（1.4 m），z 朝店内为负，靠墙的一面在 +z。</summary>
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

            var display = new ShelfDisplay { Category = cat, Pos = pos };
            for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Columns; c++)
            {
                Vector3 p = new Vector3(-0.495f + c * 0.33f, BoardCenters[r] + 0.02f, -0.01f);
                display.Items.Add(PartModel(root, cat, p));
            }
            return display;
        }

        /// <summary>一件配件的小模型，脚底在 pos，正面朝 -z。</summary>
        public static GameObject PartModel(Transform parent, PartCategory cat, Vector3 pos)
        {
            var g = new GameObject("Part_" + cat);
            Transform t = g.transform;
            t.SetParent(parent, false);
            t.localPosition = pos;

            Material dark = Mats.Lit(new Color(0.12f, 0.12f, 0.14f), 0.4f);
            Material silver = Mats.Lit(new Color(0.8f, 0.82f, 0.85f), 0.6f);
            switch (cat)
            {
                case PartCategory.Gpu:
                    Prims.Box(t, "Body", new Vector3(0, 0.05f, 0), new Vector3(0.30f, 0.09f, 0.20f), dark);
                    Prims.Shape(PrimitiveType.Cylinder, t, "FanL", new Vector3(-0.075f, 0.098f, 0), new Vector3(0.11f, 0.004f, 0.11f), silver);
                    Prims.Shape(PrimitiveType.Cylinder, t, "FanR", new Vector3(0.075f, 0.098f, 0), new Vector3(0.11f, 0.004f, 0.11f), silver);
                    Prims.Box(t, "Stripe", new Vector3(0, 0.05f, -0.102f), new Vector3(0.30f, 0.02f, 0.006f), Mats.Lit(new Color(0.85f, 0.2f, 0.2f)));
                    Prims.Box(t, "Bracket", new Vector3(-0.152f, 0.06f, 0.02f), new Vector3(0.006f, 0.12f, 0.16f), silver);
                    break;
                case PartCategory.Cpu:
                    Prims.Box(t, "Box", new Vector3(0, 0.09f, 0), new Vector3(0.20f, 0.18f, 0.12f), Mats.Lit(new Color(0.15f, 0.32f, 0.75f)));
                    Prims.Box(t, "Chip", new Vector3(0, 0.09f, -0.062f), new Vector3(0.10f, 0.10f, 0.005f), silver);
                    Prims.Box(t, "Gold", new Vector3(0, 0.09f, -0.066f), new Vector3(0.04f, 0.04f, 0.004f), Mats.Lit(new Color(0.9f, 0.7f, 0.2f), 0.7f));
                    break;
                case PartCategory.Memory:
                    for (int s = 0; s < 2; s++)
                    {
                        float z = s == 0 ? -0.05f : 0.05f;
                        Prims.Box(t, "Stick", new Vector3(0, 0.04f, z), new Vector3(0.28f, 0.07f, 0.012f), Mats.Lit(new Color(0.1f, 0.4f, 0.25f)));
                        for (int k = 0; k < 4; k++)
                            Prims.Box(t, "Chip", new Vector3(-0.09f + k * 0.06f, 0.045f, z - 0.01f), new Vector3(0.045f, 0.05f, 0.012f), dark);
                        Prims.Box(t, "Gold", new Vector3(0, 0.003f, z), new Vector3(0.26f, 0.008f, 0.014f), Mats.Lit(new Color(0.9f, 0.7f, 0.2f), 0.7f));
                    }
                    break;
                case PartCategory.Storage:
                    for (int s = 0; s < 2; s++)
                    {
                        Prims.Box(t, "Ssd", new Vector3(0, 0.015f + s * 0.034f, 0), new Vector3(0.26f, 0.03f, 0.17f), dark);
                        Prims.Box(t, "Label", new Vector3(0, 0.031f + s * 0.034f, 0), new Vector3(0.18f, 0.004f, 0.10f), Mats.Lit(new Color(0.9f, 0.6f, 0.15f)));
                    }
                    break;
                case PartCategory.Motherboard:
                    Prims.Box(t, "Box", new Vector3(0, 0.17f, 0), new Vector3(0.30f, 0.34f, 0.07f), dark);
                    Prims.Box(t, "Print", new Vector3(0, 0.17f, -0.037f), new Vector3(0.24f, 0.28f, 0.004f), Mats.Lit(new Color(0.15f, 0.55f, 0.35f)));
                    Prims.Box(t, "Socket", new Vector3(0, 0.2f, -0.041f), new Vector3(0.09f, 0.09f, 0.004f), silver);
                    break;
                default: // 电源机箱
                    Prims.Box(t, "Case", new Vector3(0, 0.25f, 0), new Vector3(0.26f, 0.5f, 0.28f), dark);
                    Prims.Box(t, "Glass", new Vector3(0, 0.27f, -0.142f), new Vector3(0.19f, 0.36f, 0.004f), Mats.Glow(new Color(0.1f, 0.3f, 0.4f), new Color(0.3f, 1.2f, 1.6f), false));
                    Prims.Box(t, "Psu", new Vector3(0, 0.04f, -0.142f), new Vector3(0.19f, 0.06f, 0.004f), Mats.Lit(new Color(0.3f, 0.3f, 0.34f)));
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
