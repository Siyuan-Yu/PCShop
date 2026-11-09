using System;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    /// <summary>
    /// 买东西的统一版式：左边商品、右边固定宽度的购物车侧栏。
    /// 侧栏高度固定，里面“装进背包 / 叫车送”两块各自滚动，选多少东西都不会挤占商品列表。
    /// </summary>
    public sealed partial class GameUi
    {
        internal const float StoreWindowWidth = 1620;
        private const float CartSideWidth = 520;

        /// <summary>左右两栏：left 放商品，right 放购物车。</summary>
        private static void BuildStoreSplit(Transform host, out RectTransform left, out RectTransform right)
        {
            var split = Ui.Row(host, 100, 12);
            split.name = "StoreSplit";
            var splitSize = split.GetComponent<LayoutElement>();
            splitSize.flexibleHeight = 1; splitSize.minHeight = 300;
            var splitLayout = split.GetComponent<HorizontalLayoutGroup>();
            splitLayout.childForceExpandHeight = true;

            left = Ui.Rect(split, "StoreGoods");
            Ui.VList(left, 6, 0);
            var leftSize = Ui.Size(left, 0, -1, 1).GetComponent<LayoutElement>();
            leftSize.minWidth = 0;

            var side = Ui.FlatPanel(split, "CartSide", Ui.FlatBand, Ui.FlatHotEdge).rectTransform;
            Ui.VList(side, 8, 12);
            var sideSize = Ui.Size(side, CartSideWidth, -1, 0).GetComponent<LayoutElement>();
            sideSize.minWidth = CartSideWidth; sideSize.flexibleWidth = 0;
            right = side;
        }

        private static Text CompactProduct(Transform row, string name, double kg, string detail, bool registered, string head = null)
        {
            string sub = head ?? (registered ? "登记为已购资产，不占背包或货车空间" : "每件 " + kg.ToString("0.###") + " 千克");
            if (!string.IsNullOrEmpty(detail)) sub += " · " + detail;
            var label = Ui.Label(row, name + "\n<size=14><color=#AAB4BB>" + sub + "</color></size>", 18, TextAnchor.MiddleLeft);
            label.lineSpacing = 1.1f;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            Ui.Size(label, -1, -1, 1); { var le = label.GetComponent<LayoutElement>(); le.minWidth = 0; le.preferredWidth = 0; }
            return label;
        }

        private RectTransform CartGroup(Transform parent, string name, string title, string info, Color edge, float? fill, bool over, out RectTransform list)
        {
            var box = Ui.FlatPanel(parent, name, Ui.FlatBand, edge).rectTransform;
            Ui.VList(box, 4, 6);
            var boxSize = Ui.Size(box, -1, -1).GetComponent<LayoutElement>();
            boxSize.flexibleHeight = 1; boxSize.minHeight = 110;
            var head = Ui.Row(box, 28, 8);
            Ui.Size(Ui.Label(head, "<b>" + title + "</b>", 18, TextAnchor.MiddleLeft, edge == Ui.FlatHotEdge ? Ui.Accent : Ui.Good), -1, 28);
            if (fill.HasValue) Ui.Bar(head, fill.Value, over ? Ui.Warn : Ui.Good, 110, 10);
            Ui.Size(Ui.Label(head, info, 15, TextAnchor.MiddleRight, over ? Ui.Warn : Ui.Dim), -1, 28, 1);
            list = GoodsList(box, 60);
            return box;
        }

        private void CartSideLine(Window w, RectTransform list, CartLine line, double amount, double kg, string moveText, double moveMax, Func<double, bool> move, Func<double, bool> remove)
        {
            var row = Ui.Row(list, 54, 6);
            row.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
            row.name = "CartLine_" + line.Key + (moveText == "改叫车" ? "_bag" : "_truck");
            string amountText = line.ByWeight ? $"{amount:0.##} 千克" : line.Key.StartsWith("ing:") ? $"{kg:0.##} 千克" : $"×{amount:0}";
            string price = line.UnitPrice != null ? " · " + line.UnitPrice().Scale(amount) : "";
            var label = Ui.Label(row, $"{line.Name} <color=#E8BE62>{amountText}</color>\n<size=14><color=#AAB4BB>{kg:0.###} 千克{price}</color></size>", 17, TextAnchor.MiddleLeft);
            label.verticalOverflow = VerticalWrapMode.Truncate;
            Ui.Size(label, -1, -1, 1); { var le = label.GetComponent<LayoutElement>(); le.minWidth = 0; le.preferredWidth = 0; }
            HoldButton(w, row, moveText, Ui.ButtonBg, moveMax, line.ByWeight ? "千克" : "件", move, 78);
            Ui.Size(Ui.Button(row, "移除", () => { remove(amount); w.Rebuild(); }, 15, Ui.ButtonBg), 58, 34);
        }

        private void CartSidebar(Window w, RectTransform side, bool defaultToShop)
        {
            var head = Ui.Row(side, 34, 8);
            int kinds = S.Cart.Count;
            Ui.Size(Ui.Label(head, kinds == 0 ? "<b>购物车</b>　还是空的"
                : $"<b>购物车</b>　{kinds} 样 · {S.CartKg:0.##} 千克", 20, TextAnchor.MiddleLeft, Ui.Accent), -1, -1, 1);
            if (kinds > 0) Ui.Size(Ui.Button(head, "清空", () => { S.ClearCart(); w.Rebuild(); }, 15, Ui.ButtonBg), 64, 30);

            bool physical = S.Cart.Any(l => !l.Registered);
            double truckKg = S.CartTruckKg;
            if (kinds == 0)
            {
                var hint = Ui.Label(side, "在左边商品点“＋”加入购物车。\n能背的优先放背包，装不下的自动叫车。\n设备、车辆直接登记为已购资产。", 16, TextAnchor.UpperLeft, Ui.Dim);
                Ui.Size(hint, -1, -1, 0).GetComponent<LayoutElement>().flexibleHeight = 1;
            }
            else
            {
                var registered = S.Cart.Where(l => l.Registered).ToArray();
                if (registered.Length > 0)
                {
                    CartGroup(side, "CartAssets", "登记为资产", "不占背包或货车", Ui.FlatCardEdge, null, false, out var assetList);
                    foreach (var item in registered)
                    {
                        var line = item; var row = Ui.Row(assetList, 46, 6);
                        row.name = "CartLine_" + line.Key + "_asset";
                        var label = Ui.Label(row, $"{line.Name} <color=#E8BE62>×{line.Amount:0.##}</color>\n<size=14><color=#AAB4BB>{line.Price}</color></size>", 17, TextAnchor.MiddleLeft);
                        label.verticalOverflow = VerticalWrapMode.Truncate;
                        Ui.Size(label, -1, -1, 1); { var le = label.GetComponent<LayoutElement>(); le.minWidth = 0; le.preferredWidth = 0; }
                        Ui.Size(Ui.Button(row, "移除", () => { S.RemoveFromCart(line.Key, line.Amount); w.Rebuild(); }, 15, Ui.ButtonBg), 58, 34);
                    }
                }
                if (physical)
                {
                    double bagKg = S.CartBagKg, free = S.FreeKg;
                    bool over = bagKg > free + 1e-9;
                    var bag = CartGroup(side, "CartBag", "装进背包", $"{bagKg:0.##} / {free:0.##} 千克", Ui.FlatCardEdge,
                        free <= 0 ? (bagKg > 0 ? 1 : 0) : (float)(bagKg / free), over, out var bagList);
                    var dest = S.CourierShop(defaultToShop);
                    int cars = S.CartCars(defaultToShop);
                    var truck = CartGroup(side, "CartTruck", "叫车送",
                        truckKg > 1e-9 ? $"{truckKg:0.##} 千克 → {(cars < 0 ? "一车装不下" : cars == 0 ? "装上刚叫的车" : cars + " 辆面包车")}" : "（没有）",
                        Ui.FlatHotEdge, null, false, out var truckList);
                    foreach (var l in S.Cart.Where(x => !x.Registered).ToList())
                    {
                        var line = l;
                        if (line.InBag > 1e-9)
                            CartSideLine(w, bagList, line, line.InBag, line.BagKg, "改叫车", line.InBag, n => S.CartToTruck(line.Key, n), n => S.CartToTruck(line.Key, n) && S.RemoveFromCart(line.Key, n));
                        if (line.ByTruck > 1e-9)
                            CartSideLine(w, truckList, line, line.ByTruck, line.TruckKg, "放背包", line.ByTruck, n => S.CartToBag(line.Key, n), n => S.RemoveFromCart(line.Key, n));
                    }
                    if (bagKg <= 1e-9) Para(bagList, "新购商品默认优先放背包。", 14, Ui.Dim);
                    var destRow = Ui.Row(side, 34, 8);
                    if (truckKg > 1e-9)
                    {
                        var (_, minutes) = S.CourierQuote(dest);
                        Ui.Size(Ui.Label(destRow, "送到", 16, TextAnchor.MiddleLeft, Ui.Dim), 40);
                        Ui.Size(Ui.Button(destRow, S.CourierDestName(dest) + "  ▼", () => OpenCourierDestMenu(w, dest, false), 15, Ui.ButtonBg), -1, 32, 1);
                        Ui.Size(Ui.Label(destRow, $"约 {minutes} 分钟", 14, TextAnchor.MiddleRight, Ui.Dim), 80);
                    }
                    else Ui.Size(Ui.Label(destRow, "全部自己背走，不叫车。", 15, TextAnchor.MiddleLeft, Ui.Dim), -1, -1, 1);
                    var quick = Ui.Row(side, 32, 8);
                    Ui.Size(Ui.Button(quick, "背得动的放背包", () => { S.CartFillBag(); w.Rebuild(); }, 15, Ui.ButtonBg), -1, 32, 1);
                    Ui.Size(Ui.Button(quick, "全部叫车", () => { S.CartAllToTruck(); w.Rebuild(); }, 15, Ui.ButtonBg), -1, 32, 1);
                }
            }

            Money goods = S.CartGoods;
            Money freight = physical && truckKg > 1e-9 ? S.CartFreight(defaultToShop) : Money.Zero;
            string freightText = !physical || truckKg <= 1e-9 ? "运费 ¥0"
                : S.CartCars(defaultToShop) > 0 ? $"运费 {freight}（{S.CartCars(defaultToShop)} 辆 × {S.CourierQuote(S.CourierShop(defaultToShop)).fee}）"
                : S.CartCars(defaultToShop) == 0 ? "运费 ¥0（装上刚叫的车）" : "运费：有一件一车装不下";
            var bill = Ui.Row(side, 40, 8);
            bill.name = "CartBill";
            Ui.Size(Ui.Label(bill, $"货款 {goods}　{freightText}", 15, TextAnchor.MiddleLeft, Ui.Dim), -1, 40, 1);
            Ui.Size(Ui.Label(bill, $"<b><color=#E8C77A>{goods + freight}</color></b>", 28, TextAnchor.MiddleRight), 190, 40);

            string why = kinds == 0 ? "购物车是空的" : S.CheckoutBlocker(defaultToShop);
            var checkout = Ui.Button(side, why ?? "结账（付合计）", () => { S.Checkout(defaultToShop); w.Rebuild(); },
                why == null ? 22 : 15, why == null ? Ui.ButtonHot : Ui.ButtonBg, why == null);
            checkout.name = "CartCheckout";
            Ui.Size(checkout, -1, 54);
        }
    }
}
