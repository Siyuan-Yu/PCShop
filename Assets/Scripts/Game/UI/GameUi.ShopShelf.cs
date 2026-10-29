using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Shop;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    /// <summary>货架窗口和整机展示台窗口：画成真的货架 / 展台的样子，货摆在板子上，价签挂在板边。</summary>
    public sealed partial class GameUi
    {
        private static readonly Color WoodDark = new Color(0.33f, 0.22f, 0.13f);
        private static readonly Color WoodLight = new Color(0.58f, 0.41f, 0.24f);
        private static readonly Color ShelfBack = new Color(0.16f, 0.14f, 0.13f);
        private static readonly Color TagPaper = new Color(0.95f, 0.93f, 0.85f);
        private static readonly Color TagInk = new Color(0.16f, 0.16f, 0.18f);
        private static readonly Color CardboardColor = new Color(0.78f, 0.62f, 0.4f);

        private static Color PartColor(PartCategory c)
        {
            switch (c)
            {
                case PartCategory.Gpu: return new Color(0.14f, 0.14f, 0.17f);
                case PartCategory.Cpu: return new Color(0.88f, 0.57f, 0.17f);
                case PartCategory.Memory: return new Color(0.2f, 0.65f, 0.35f);
                case PartCategory.Storage: return new Color(0.25f, 0.45f, 0.85f);
                case PartCategory.PowerCase: return new Color(0.72f, 0.74f, 0.78f);
                default: return new Color(0.62f, 0.2f, 0.26f);
            }
        }

        private static RectTransform Chip(Transform parent, Color c, float x0, float y0, float x1, float y1)
        {
            Image img = Ui.Panel(parent, "Chip", c);
            return img.rectTransform.Anchor(x0, y0, x1, y1);
        }

        /// <summary>货架图标的比例尺：每厘米多少像素。所有配件用同一把尺，大小关系才对。</summary>
        private const float CmPx = 2.4f;

        private static Sprite _circle;

        private static Sprite CircleSprite()
        {
            if (_circle != null) return _circle;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f));
                tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(n / 2f - d)));
            }
            tex.Apply();
            _circle = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            return _circle;
        }

        /// <summary>像素矩形：坐标从父节点左下角算起。</summary>
        private static void Rc(Transform parent, Color c, float x, float y, float w, float h)
        {
            RectTransform rt = Ui.Panel(parent, "r", c).rectTransform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
        }

        /// <summary>像素圆：圆心坐标 + 直径。</summary>
        private static void Dot(Transform parent, Color c, float cx, float cy, float d)
        {
            Image img = Ui.Panel(parent, "o", c);
            img.sprite = CircleSprite();
            RectTransform rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(cx, cy);
            rt.sizeDelta = new Vector2(d, d);
        }

        private static void Fan(Transform p, float cx, float cy, float d)
        {
            Dot(p, new Color(0.3f, 0.3f, 0.35f), cx, cy, d);
            Dot(p, new Color(0.09f, 0.09f, 0.11f), cx, cy, d * 0.8f);
            Rc(p, new Color(0.45f, 0.46f, 0.5f), cx - d * 0.36f, cy - 0.5f, d * 0.72f, 1f);
            Rc(p, new Color(0.45f, 0.46f, 0.5f), cx - 0.5f, cy - d * 0.36f, 1f, d * 0.72f);
            Dot(p, new Color(0.75f, 0.76f, 0.8f), cx, cy, d * 0.26f);
        }

        /// <summary>配件的真实尺寸（厘米，正面看过去的宽 × 高）。散件是配件本体，整盒是零售包装盒。</summary>
        private static Vector2 PartCm(PartCategory cat, bool boxed)
        {
            switch (cat)
            {
                case PartCategory.Gpu: return boxed ? new Vector2(38, 17) : new Vector2(30, 13);
                case PartCategory.Cpu: return boxed ? new Vector2(12, 13) : new Vector2(4.5f, 4.5f);
                case PartCategory.Memory: return boxed ? new Vector2(15, 8) : new Vector2(13.3f, 3.2f);
                case PartCategory.Storage: return boxed ? new Vector2(14, 10) : new Vector2(10, 7);
                case PartCategory.PowerCase: return boxed ? new Vector2(24, 48) : new Vector2(21, 46);
                default: return boxed ? new Vector2(34, 28) : new Vector2(30, 24.5f);
            }
        }

        private static Vector2 IconSize(PartCategory cat, bool boxed)
        {
            Vector2 s = PartCm(cat, boxed) * CmPx;
            return boxed ? s : new Vector2(Mathf.Max(s.x, 10f), Mathf.Max(s.y, 7f));
        }

        /// <summary>配件本体的插画，画在 w×h 像素里（比例按真实尺寸）。</summary>
        private static void DrawPart(Transform root, PartCategory cat, float w, float h)
        {
            var gold = new Color(0.86f, 0.7f, 0.27f);
            var silver = new Color(0.82f, 0.84f, 0.88f);
            var dark = new Color(0.09f, 0.09f, 0.11f);
            void F(Color c, float x0, float y0, float x1, float y1) => Rc(root, c, x0 * w, y0 * h, (x1 - x0) * w, (y1 - y0) * h);
            switch (cat)
            {
                case PartCategory.Gpu:
                    F(silver, 0, 0, 0.05f, 1);
                    F(new Color(0.14f, 0.14f, 0.17f), 0.05f, 0.04f, 1, 0.96f);
                    F(new Color(0.9f, 0.25f, 0.25f), 0.05f, 0.8f, 1, 0.92f);
                    F(gold, 0.14f, 0, 0.6f, 0.1f);
                    Fan(root, w * 0.31f, h * 0.46f, h * 0.66f);
                    Fan(root, w * 0.7f, h * 0.46f, h * 0.66f);
                    break;
                case PartCategory.Cpu:
                    F(new Color(0.24f, 0.5f, 0.34f), 0, 0, 1, 1);
                    F(silver, 0.17f, 0.17f, 0.83f, 0.83f);
                    F(new Color(0.93f, 0.94f, 0.96f), 0.3f, 0.3f, 0.7f, 0.7f);
                    F(gold, 0, 0, 0.22f, 0.22f);
                    break;
                case PartCategory.Memory:
                    F(new Color(0.14f, 0.55f, 0.3f), 0, 0.08f, 1, 0.9f);
                    F(new Color(0.8f, 0.82f, 0.86f), 0, 0.9f, 1, 1);
                    for (int i = 0; i < 4; i++) F(dark, 0.07f + i * 0.22f, 0.3f, 0.23f + i * 0.22f, 0.8f);
                    F(gold, 0, 0, 1, 0.2f);
                    break;
                case PartCategory.Storage:
                    F(new Color(0.22f, 0.4f, 0.8f), 0, 0, 0.88f, 1);
                    F(new Color(0.14f, 0.28f, 0.6f), 0, 0, 0.88f, 0.16f);
                    F(new Color(0.94f, 0.94f, 0.96f), 0.1f, 0.3f, 0.7f, 0.85f);
                    F(new Color(0.2f, 0.22f, 0.3f), 0.15f, 0.55f, 0.55f, 0.68f);
                    F(gold, 0.88f, 0.15f, 1, 0.85f);
                    break;
                case PartCategory.PowerCase:
                    F(new Color(0.74f, 0.76f, 0.8f), 0, 0, 1, 1);
                    F(new Color(0.5f, 0.52f, 0.56f), 0, 0.93f, 1, 1);
                    F(dark, 0.14f, 0.82f, 0.86f, 0.85f);
                    F(dark, 0.14f, 0.77f, 0.86f, 0.8f);
                    F(dark, 0.08f, 0.02f, 0.92f, 0.14f);
                    Fan(root, w * 0.5f, h * 0.42f, w * 0.62f);
                    Dot(root, new Color(0.3f, 0.9f, 0.5f), w * 0.8f, h * 0.2f, 4);
                    break;
                default:
                    F(new Color(0.55f, 0.18f, 0.24f), 0, 0, 1, 1);
                    F(silver, 0.2f, 0.45f, 0.45f, 0.75f);
                    F(dark, 0.2f, 0.45f, 0.45f, 0.49f);
                    F(dark, 0.55f, 0.22f, 0.59f, 0.92f);
                    F(dark, 0.62f, 0.22f, 0.66f, 0.92f);
                    F(dark, 0.08f, 0.15f, 0.5f, 0.2f);
                    F(silver, 0.72f, 0.1f, 0.84f, 0.25f);
                    F(gold, 0.86f, 0.1f, 0.95f, 0.35f);
                    F(silver, 0, 0.55f, 0.08f, 0.95f);
                    break;
            }
        }

        /// <summary>一件货的图标：散件是配件本体；整盒是纸箱，正面印着配件图。大小按真实尺寸。</summary>
        private static RectTransform PartIcon(Transform parent, PartCategory cat, bool boxed, float alpha = 1f)
        {
            Vector2 loose = IconSize(cat, false);
            RectTransform root = Ui.Rect(parent, "Icon");
            if (alpha < 1f) root.gameObject.AddComponent<CanvasGroup>().alpha = alpha;
            if (!boxed)
            {
                root.sizeDelta = loose;
                DrawPart(root, cat, loose.x, loose.y);
                return root;
            }
            Vector2 box = PartCm(cat, true) * CmPx;
            root.sizeDelta = box;
            Rc(root, new Color(0.78f, 0.62f, 0.4f), 0, 0, box.x, box.y);
            Rc(root, new Color(0.62f, 0.47f, 0.28f), 0, box.y * 0.86f, box.x, box.y * 0.14f);
            Rc(root, PartColor(cat), 0, 0, box.x, Mathf.Max(3f, box.y * 0.1f));
            float rx = box.x * 0.08f, ry = box.y * 0.14f, rw = box.x * 0.84f, rh = box.y * 0.68f;
            Rc(root, new Color(0.96f, 0.94f, 0.88f), rx, ry, rw, rh);
            float s = Mathf.Min(rw * 0.86f / loose.x, rh * 0.86f / loose.y);
            RectTransform pic = Ui.Rect(root, "Pic");
            pic.anchorMin = pic.anchorMax = Vector2.zero;
            pic.pivot = new Vector2(0.5f, 0.5f);
            pic.sizeDelta = loose;
            pic.anchoredPosition = new Vector2(rx + rw / 2, ry + rh / 2);
            pic.localScale = new Vector3(s, s, 1);
            DrawPart(pic, cat, loose.x, loose.y);
            return root;
        }
        /// <summary>价签：米色小牌子，深色字。</summary>
        private static void PriceTag(Transform parent, string text, float x0, float y0, float x1, float y1, int size = 15)
        {
            Image tag = Ui.Panel(parent, "Tag", TagPaper);
            tag.rectTransform.Anchor(x0, y0, x1, y1);
            Text t = Ui.Label(tag.transform, text, size, TextAnchor.MiddleCenter, TagInk);
            t.rectTransform.Fill(2);
        }

        // ---------- 货架 ----------

        /// <summary>走到货架前按 F：一座木货架，上层摆整盒、下层摆散件，入门 / 主流 / 高端各一格，价签在板边。</summary>
        public void OpenShelfWindow(PartCategory cat)
        {
            OpenWindow("货架：" + PartCatalog.CategoryName(cat), 980, 740, w =>
            {
                ShopSimulation shop = S.PlayerShop;
                Stock st = shop.Stock;
                var parts = PartCatalog.InCategory(cat).OrderBy(p => p.Tier).ToList();
                int count = parts.Sum(p => st.CountAll(p));
                Line(w.Content, $"整店仓位 {st.UsedSpace:0.#}/{st.Capacity:0.#} 格，还剩 {st.FreeSpace:0.#} 格（所有货架共用）。"
                    + (count == 0 ? "  <color=#F07A5A>这个货架空了，只摆着样品。</color>" : ""), 16, Ui.Dim);

                Image frame = Ui.Panel(w.Content, "Shelf", WoodDark);
                Ui.Size(frame, -1, 540);
                Ui.VList(frame.rectTransform, 0, 12);

                // 横梁招牌
                Image sign = Ui.Panel(frame.transform, "Sign", WoodLight);
                Ui.Size(sign, -1, 34);
                Text st1 = Ui.Label(sign.transform, "<b>" + PartCatalog.CategoryName(cat) + "</b>　整盒在上层，散件在下层", 18, TextAnchor.MiddleCenter, new Color(0.18f, 0.11f, 0.05f));
                st1.rectTransform.Fill(2);
                Ui.Size(Ui.Panel(frame.transform, "Gap", WoodDark), -1, 6);

                for (int board = 0; board < 2; board++)
                {
                    bool boxed = board == 0;
                    RectTransform row = Ui.Row(frame.transform, 205, 8);
                    foreach (PartDef p in parts)
                        BuildShelfSlot(row, p, boxed, shop);
                    Ui.Size(Ui.Panel(frame.transform, "Plank", WoodLight), -1, 14);
                    Ui.Size(Ui.Panel(frame.transform, "Gap", WoodDark), -1, 8);
                }

                Ui.Size(Ui.Button(w.Content, "关闭", w.Close, 20), -1, 46);
            });
        }

        private void BuildShelfSlot(Transform row, PartDef p, bool boxed, ShopSimulation shop)
        {
            int have = shop.Stock.Count(p, boxed);
            Image slot = Ui.Panel(row, "Slot", ShelfBack);
            Ui.Size(slot, 296, 205);
            RectTransform rt = slot.rectTransform;

            Color[] tierCol = { new Color(0.55f, 0.58f, 0.64f), new Color(0.2f, 0.7f, 0.65f), new Color(0.9f, 0.7f, 0.25f) };
            Chip(rt, tierCol[Mathf.Clamp(p.Tier, 0, 2)], 0, 0.975f, 1, 1);
            Text name = Ui.Label(rt, $"<b>{PartCatalog.TierName(p.Tier)}</b>　<size=13>{p.Name}</size>", 15, TextAnchor.MiddleLeft, new Color(0.85f, 0.87f, 0.9f));
            name.rectTransform.Anchor(0, 0.84f, 1, 1, 10, 0, 6, 0);

            // 货放在板子上：从板面往上一层层摆，大小按真实尺寸
            Chip(rt, WoodLight, 0, 0.255f, 1, 0.272f);
            RectTransform items = Ui.Rect(rt, "Items").Anchor(0, 0.272f, 1, 0.84f, 8, 0, 8, 0);
            const float gx = 8, gy = 5, areaW = 280, areaH = 100;
            Vector2 cell = IconSize(p.Category, boxed);
            int cols = Mathf.Max(1, Mathf.FloorToInt((areaW + gx) / (cell.x + gx)));
            int rowsN = Mathf.Max(1, Mathf.FloorToInt((areaH + gy) / (cell.y + gy)));
            int cap = Mathf.Min(cols * rowsN, 12);
            int slots = have == 0 ? 1 : Mathf.Min(have, cap);
            for (int i = 0; i < slots; i++)
            {
                int cx = i % cols, cy = i / cols;
                RectTransform ic = PartIcon(items, p.Category, boxed, have == 0 ? 0.22f : 1f);
                ic.anchorMin = ic.anchorMax = Vector2.zero;
                ic.pivot = Vector2.zero;
                ic.anchoredPosition = new Vector2(4 + cx * (cell.x + gx), 2 + cy * (cell.y + gy));
            }
            if (have == 0)
            {
                Text none = Ui.Label(items, "缺货", 22, TextAnchor.MiddleCenter, new Color(0.94f, 0.48f, 0.35f));
                none.rectTransform.Anchor(0.5f, 0, 1, 1);
            }
            else if (have > cap)
            {
                Text more = Ui.Label(items, "…", 20, TextAnchor.MiddleCenter, new Color(0.8f, 0.8f, 0.82f));
                more.rectTransform.Anchor(0.9f, 0.7f, 1, 1);
            }

            string tag = have == 0
                ? "<color=#B33A2A>缺货</color>"
                : $"<b>{shop.PriceOf(p, boxed)}</b>　×{have}";
            // 标记：整盒（纸箱色）/ 散件（蓝灰色）
            Image mark = Ui.Panel(rt, "Mark", boxed ? CardboardColor : new Color(0.36f, 0.5f, 0.68f));
            mark.rectTransform.Anchor(0.04f, 0.04f, 0.27f, 0.23f);
            Ui.Label(mark.transform, boxed ? "整盒" : "散件", 16, TextAnchor.MiddleCenter, boxed ? new Color(0.25f, 0.15f, 0.05f) : Color.white)
                .rectTransform.Fill(2);
            PriceTag(rt, tag, 0.29f, 0.04f, 0.96f, 0.23f, 16);
        }
        // ---------- 整机展示台 ----------

        /// <summary>展示台上的整机：三台开着的整机并排，屏幕上是档次和成交价，台面下的牌子写配置、配件齐不齐。</summary>
        public void OpenDisplayWindow()
        {
            OpenWindow("整机展示台", 960, 720, w =>
            {
                ShopSimulation shop = S.PlayerShop;
                Line(w.Content, "顾客看中展台上的整机，会下装机单：离店后两天内你按配置装好才收钱（超时取消、掉知名度）。配件从库存里出，先备齐。", 15, Ui.Dim);

                Image table = Ui.Panel(w.Content, "Table", new Color(0.11f, 0.12f, 0.14f));
                Ui.Size(table, -1, 470);
                RectTransform row = Ui.Row(table.transform, 470, 10);
                row.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(10, 10, 10, 10);
                row.Fill();
                for (int tier = 0; tier < 3; tier++) BuildDisplayStand(row, tier, shop);

                int orders = shop.Orders.Count;
                Line(w.Content, orders > 0 ? $"现在有 {orders} 个装机单等你装。" : "现在没有装机单。", 17, orders > 0 ? Ui.Warn : Ui.Dim);
                RectTransform bar = Ui.Row(w.Content, 46);
                if (orders > 0)
                    Ui.Size(Ui.Button(bar, "去处理装机单", () =>
                    {
                        w.Close();
                        OpenDock();
                        SelectTab(ShopTab.Orders);
                    }, 19, Ui.ButtonHot), 300);
                Ui.Size(Ui.Button(bar, "关闭", w.Close, 19), 200);
            });
        }

        private void BuildDisplayStand(Transform row, int tier, ShopSimulation shop)
        {
            var parts = PartCatalog.BuildList(tier);
            var missing = parts.Where(p => shop.Stock.CountAll(p) == 0).ToList();
            bool ready = missing.Count == 0;
            Color[] screens =
            {
                new Color(0.16f, 0.38f, 0.66f),
                new Color(0.14f, 0.5f, 0.48f),
                new Color(0.42f, 0.24f, 0.62f),
            };

            Image col = Ui.Panel(row, "Stand", new Color(0.16f, 0.17f, 0.2f));
            Ui.Size(col, 294, 450);
            RectTransform rt = col.rectTransform;

            // 显示器
            Image bezel = Ui.Panel(rt, "Bezel", new Color(0.05f, 0.05f, 0.07f));
            bezel.rectTransform.Anchor(0.06f, 0.56f, 0.94f, 0.98f);
            Image screen = Ui.Panel(bezel.transform, "Screen", screens[tier]);
            screen.rectTransform.Fill(7);
            Text sn = Ui.Label(screen.transform, $"<b>{PartCatalog.TierName(tier)}整机</b>", 26, TextAnchor.MiddleCenter);
            sn.rectTransform.Anchor(0, 0.55f, 1, 1);
            Text sp = Ui.Label(screen.transform, shop.BuildPrice(tier).ToString(), 30, TextAnchor.MiddleCenter, new Color(0.7f, 1f, 0.75f));
            sp.rectTransform.Anchor(0, 0.25f, 1, 0.58f);
            Text ss = Ui.Label(screen.transform, ready ? "配件齐，随时能装" : $"还缺 {missing.Count} 样配件", 16,
                TextAnchor.MiddleCenter, ready ? new Color(0.85f, 1f, 0.88f) : new Color(1f, 0.78f, 0.65f));
            ss.rectTransform.Anchor(0, 0, 1, 0.26f);
            // 支架 + 键盘 + 台面
            Chip(rt, new Color(0.3f, 0.3f, 0.34f), 0.44f, 0.52f, 0.56f, 0.56f);
            Chip(rt, new Color(0.3f, 0.3f, 0.34f), 0.34f, 0.5f, 0.66f, 0.525f);
            Chip(rt, WoodLight, 0, 0.455f, 1, 0.495f);
            Chip(rt, ready ? Ui.Good : Ui.Warn, 0.9f, 0.462f, 0.96f, 0.478f);

            // 展牌：配置清单
            Image card = Ui.Panel(rt, "Card", TagPaper);
            card.rectTransform.Anchor(0.04f, 0.02f, 0.96f, 0.43f);
            Money cost = Money.Zero;
            foreach (PartDef p in parts) cost += S.Market.PriceOf(p);
            var sb = new System.Text.StringBuilder();
            foreach (PartDef p in parts)
            {
                int have = shop.Stock.CountAll(p);
                string mark = have > 0 ? "<color=#1E8E3E>有</color>" : "<color=#C62828>缺</color>";
                string pn = p.Name.Replace("电源机箱套装", "").Trim();
                sb.Append($"{mark} <b>{PartCatalog.CategoryName(p.Category)}</b> {pn}  ×{have}\n");
            }
            sb.Append($"<size=13><color=#6B6F78>配件市价合计 {cost}</color></size>");
            Text ct = Ui.Label(card.transform, sb.ToString(), 14, TextAnchor.MiddleLeft, TagInk);
            ct.rectTransform.Fill(8);
        }
    }
}
