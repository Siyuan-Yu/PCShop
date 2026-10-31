using System;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    /// <summary>购物车 / 装货单里能拖的一行：拖到另一栏松手就挪过去一件。</summary>
    public sealed class CartDragItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public Action<Vector2, Camera> Dropped;
        private RectTransform _rt;
        private Transform _home;
        private int _index;
        private CanvasGroup _group;
        private Transform _layer;

        private void Awake()
        {
            _rt = (RectTransform)transform;
            _group = gameObject.AddComponent<CanvasGroup>();
        }

        public void OnBeginDrag(PointerEventData e)
        {
            _home = transform.parent;
            _index = transform.GetSiblingIndex();
            Canvas canvas = GetComponentInParent<Canvas>();
            _layer = canvas != null ? canvas.rootCanvas.transform : transform.root;
            Vector2 size = _rt.rect.size;
            transform.SetParent(_layer, true);
            _rt.sizeDelta = size;
            _group.blocksRaycasts = false;
            _group.alpha = 0.85f;
        }

        public void OnDrag(PointerEventData e)
        {
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)_layer, e.position, e.pressEventCamera, out Vector3 world))
                _rt.position = world;
        }

        public void OnEndDrag(PointerEventData e)
        {
            _group.blocksRaycasts = true;
            _group.alpha = 1;
            transform.SetParent(_home, false);
            transform.SetSiblingIndex(_index);
            Dropped?.Invoke(e.position, e.pressEventCamera);
        }
    }

    public sealed partial class GameUi
    {
        private static readonly Color CartColumn = new Color(0.12f, 0.15f, 0.18f);
        private static readonly Color CartCard = new Color(0.19f, 0.23f, 0.27f);

        /// <summary>商品行右边的“加入购物车”按钮；showCount 时前面写购物车里已经有几件。</summary>
        private void CartAddButton(Window w, Transform row, string key, Func<bool> add, string label, float width, bool enabled = true, bool showCount = true)
        {
            if (showCount)
            {
                double n = S.CartAmount(key);
                Ui.Size(Ui.Label(row, n > 0 ? $"购物车 ×{n:0}" : "", 14, TextAnchor.MiddleRight, Ui.Accent), 90);
            }
            Ui.Size(Ui.Button(row, label, () =>
            {
                add();
                w.Rebuild();
            }, 16, Ui.ButtonHot, enabled), width);
        }

        /// <summary>
        /// 购物车和结账：左栏“叫车送”，右栏“装进背包”。东西加进来先都在左栏，拖到右栏（或点 ›）就自己背，
        /// 超重了拖回去；左栏剩下的一起叫车，按总重算几辆车，运费和货款一起结。
        /// </summary>
        private void CartPanel(Window w, Transform parent, bool defaultToShop)
        {
            RectTransform head = Ui.Row(parent, 34, 8);
            int kinds = S.Cart.Count;
            Ui.Size(Ui.Label(head, kinds == 0 ? "<b>购物车</b>　还是空的，在上面点“加入购物车”"
                : $"<b>购物车</b>　{kinds} 样 · 共 {S.CartKg:0.#} 公斤 · 货款 {S.CartGoods}", 18, TextAnchor.MiddleLeft, Ui.Accent), -1, -1, 1);
            if (kinds == 0) return;
            Ui.Size(Ui.Button(head, "清空", () => { S.ClearCart(); w.Rebuild(); }, 15, Ui.ButtonBg), 70);

            RectTransform cols = Ui.Row(parent, 190, 10);
            RectTransform truckCol = CartColumnBox(cols, out RectTransform truckList);
            RectTransform bagCol = CartColumnBox(cols, out RectTransform bagList);

            OwnedShop dest = S.CourierShop(defaultToShop);
            (Money fee, int minutes) = S.CourierQuote(dest);
            int cars = S.CartCars(defaultToShop);
            double truckKg = S.CartTruckKg;
            Line(truckList, truckKg > 1e-9
                ? $"叫车送　{truckKg:0.#} 公斤 → {(cars < 0 ? "有一件一车装不下" : cars == 0 ? "装上刚叫的车" : $"{cars} 辆面包车")}"
                : "叫车送　（没有）", 16, Ui.Accent);
            double bagKg = S.CartBagKg, free = S.FreeKg;
            bool over = bagKg > free + 1e-9;
            RectTransform bagHead = Ui.Row(bagList, 24, 8);
            Ui.Size(Ui.Label(bagHead, $"装进背包　{bagKg:0.#} / {free:0.#} 公斤", 16, TextAnchor.MiddleLeft, over ? Ui.Warn : Ui.Accent), -1, -1, 1);
            Ui.Bar(bagHead, free <= 0 ? (bagKg > 0 ? 1 : 0) : (float)(bagKg / free), over ? Ui.Warn : Ui.Good, 120, 10);

            foreach (CartLine l in S.Cart.ToList())
            {
                CartLine line = l;
                const double one = 1;
                if (line.ByTruck > 1e-9)
                    CartLineCard(w, truckList, line, line.ByTruck, line.TruckKg, bagCol, () => S.CartToBag(line.Key, Math.Min(one, line.ByTruck)),
                        ("›", () => S.CartToBag(line.Key, one)), ("»", () => S.CartToBag(line.Key, line.ByTruck)), ("−", () => S.RemoveFromCart(line.Key, Math.Min(one, line.Amount))));
                if (line.InBag > 1e-9)
                    CartLineCard(w, bagList, line, line.InBag, line.BagKg, truckCol, () => S.CartToTruck(line.Key, Math.Min(one, line.InBag)),
                        ("‹", () => S.CartToTruck(line.Key, one)), ("«", () => S.CartToTruck(line.Key, line.InBag)), ("−", () => S.CartToTruck(line.Key, one) && S.RemoveFromCart(line.Key, one)));
            }
            if (bagKg <= 1e-9) Para(bagList, "把左边的拖到这里（或点 ›）就自己背走；超重了再拖回去。", 14, Ui.Dim);

            RectTransform destRow = Ui.Row(parent, 40, 8);
            if (truckKg > 1e-9)
            {
                Ui.Size(Ui.Label(destRow, "送到", 16, TextAnchor.MiddleLeft, Ui.Dim), 40);
                Ui.Size(Ui.Button(destRow, S.CourierDestName(dest) + "  ▼", () => OpenCourierDestMenu(w, dest, false), 15, new Color(0.2f, 0.24f, 0.3f)), 300);
                Ui.Size(Ui.Label(destRow, $"每辆 {fee}（最多 {GameSession.CourierCapacityKg:0} 公斤），约 {minutes} 分钟到" + (cars > 0 ? $"；运费 {fee * cars}" : ""), 15, TextAnchor.MiddleLeft, Ui.Dim), -1, -1, 1);
            }
            else Ui.Size(Ui.Label(destRow, "全部自己背走，不叫车。", 15, TextAnchor.MiddleLeft, Ui.Dim), -1, -1, 1);

            RectTransform pay = Ui.Row(parent, 46, 8);
            Ui.Size(Ui.Button(pay, "背得动的都放背包", () => { S.CartFillBag(); w.Rebuild(); }, 15, Ui.ButtonBg), 170);
            Ui.Size(Ui.Button(pay, "全部叫车", () => { S.CartAllToTruck(); w.Rebuild(); }, 15, Ui.ButtonBg), 110);
            string why = S.CheckoutBlocker(defaultToShop);
            Money total = S.CartGoods + (truckKg > 1e-9 ? S.CartFreight(defaultToShop) : Money.Zero);
            var checkout = Ui.Button(pay, why ?? $"结账 {total}", () =>
            {
                S.Checkout(defaultToShop);
                w.Rebuild();
            }, why == null ? 18 : 14, why == null ? Ui.ButtonHot : Ui.ButtonBg, why == null);
            checkout.name = "CartCheckout";
            Ui.Size(checkout, -1, -1, 1);
        }

        private static RectTransform CartColumnBox(Transform row, out RectTransform list)
        {
            Image box = Ui.Panel(row, "CartColumn", CartColumn);
            Ui.Size(box, -1, 190, 1);
            list = Ui.Scroll(box.transform, -1);
            ((RectTransform)list.parent.parent).Fill();
            return box.rectTransform;
        }

        /// <summary>一张能拖的卡片：名字、数量、重量，后面几个小按钮；拖到 target 栏松手执行 onDrop。</summary>
        private void CartLineCard(Window w, RectTransform list, CartLine l, double amount, double kg, RectTransform target, Func<bool> onDrop,
            params (string text, Func<bool> act)[] buttons)
        {
            RectTransform card = Ui.Row(list, 34, 4);
            Image bg = card.gameObject.AddComponent<Image>();
            bg.color = CartCard;
            string amountText = l.ByJin ? $"{amount:0.#} 斤" : $"×{amount:0}";
            string price = l.UnitPrice != null ? $"　{l.UnitPrice().Scale(amount)}" : "";
            Ui.Size(Ui.Label(card, $"  {l.Name} {amountText}<size=13><color=#A8B0BA>　{kg:0.#} 公斤{price}</color></size>", 15), -1, -1, 1);
            foreach (var (text, act) in buttons)
            {
                var a = act;
                Ui.Size(Ui.Button(card, text, () => { a(); w.Rebuild(); }, 16, Ui.ButtonBg), 32);
            }
            var drag = card.gameObject.AddComponent<CartDragItem>();
            drag.Dropped = (pos, cam) =>
            {
                if (!RectTransformUtility.RectangleContainsScreenPoint(target, pos, cam)) return;
                onDrop();
                w.Rebuild();
            };
        }

        /// <summary>“送到”下拉框：家里 + 每一家自己的店，各自的运费和大概多久到。forTruck 时不能选现在站的地方。</summary>
        private void OpenCourierDestMenu(Window owner, OwnedShop current, bool forTruck)
        {
            int n = 1 + S.MyShopCount;
            OpenWindow("送到哪", 600, 150 + n * 72, w =>
            {
                void Option(OwnedShop shop)
                {
                    (Money fee, int minutes) = S.CourierQuote(shop);
                    bool here = forTruck && (shop == null ? S.CurrentPlace == S.Home : S.CurrentPlace == shop.Place);
                    bool chosen = shop == current;
                    string sub = here ? "你就在这里" : $"每辆运费 {fee}，约 {minutes} 分钟到";
                    Ui.Size(Ui.Button(w.Content, $"{S.CourierDestName(shop)}{(chosen ? "  ✓" : "")}\n<size=13>{sub}</size>", () =>
                    {
                        S.SetCourierDest(shop);
                        w.Close();
                        owner?.Rebuild();
                    }, 18, chosen ? Ui.ButtonHot : Ui.ButtonBg, !here), -1, 64);
                }
                Option(null);
                foreach (OwnedShop s in S.MyShops) Option(s);
                if (!S.HasShop) Line(w.Content, "还没有店铺，去房产中介租一间才能送店里。", 15, Ui.Dim);
                Ui.Size(Ui.Button(w.Content, "取消", w.Close, 18), -1, 44);
            });
        }

        /// <summary>1788 等线上下单送店里时的名字：当前这家店。</summary>
        private string ShopDestName => S.HasShop ? S.CourierDestName(S.CurrentShop) : "我的店里";

        // ---------- 打电话叫货车 ----------

        /// <summary>
        /// 打电话叫货车：左边是这里能交给司机的东西，拖到右边装货单（或点“装”），选好送到哪，点出发一起付运费。
        /// </summary>
        public void OpenTruckWindow()
        {
            OpenWindow("叫货车 · " + GameSession.TruckDriverName, 1180, 860, w =>
            {
                Para(w.Content, $"{GameSession.TruckDriverName}开面包车，每辆最多装 {GameSession.CourierCapacityKg:0} 公斤，运费按路程算。身上的东西在哪都能交给他；在家还能搬厨房和纸箱里的，在自己店里还能搬后仓的货和储物柜。" +
                    "店里摆着的家具先在摆放模式收回“我的”——“我的”里的家具到哪家店都能直接摆，不用搬。", 15, Ui.Dim);
                OwnedShop dest = S.TruckDest;
                RectTransform destRow = Ui.Row(w.Content, 42, 8);
                Ui.Size(Ui.Label(destRow, "送到", 17, TextAnchor.MiddleLeft, Ui.Dim), 44);
                Ui.Size(Ui.Button(destRow, S.CourierDestName(dest) + "  ▼", () => OpenCourierDestMenu(w, dest, true), 16, new Color(0.2f, 0.24f, 0.3f)), 320);
                string destWhy = S.TruckDestBlocker;
                (Money fee, int minutes) = S.CourierQuote(dest);
                Ui.Size(Ui.Label(destRow, destWhy ?? $"每辆 {fee}，约 {minutes} 分钟到", 15, TextAnchor.MiddleLeft, destWhy != null ? Ui.Warn : Ui.Dim), -1, -1, 1);

                RectTransform cols = Ui.Row(w.Content, 520, 10);
                RectTransform hereCol = CartColumnBox(cols, out RectTransform hereList);
                RectTransform loadCol = CartColumnBox(cols, out RectTransform loadList);
                Ui.Size(hereCol, -1, 520, 1);
                Ui.Size(loadCol, -1, 520, 1);
                Line(hereList, "这里能搬的", 17, Ui.Accent);
                var items = S.MovableHere();
                if (items.Count == 0) Para(hereList, "这里没有能交给司机的东西。", 15, Ui.Dim);
                foreach (MovableItem m in items)
                {
                    MovableItem it = m;
                    double left = it.Have - S.TruckAmount(it.Key);
                    if (left <= 1e-9) continue;
                    RectTransform card = Ui.Row(hereList, 38, 4);
                    card.gameObject.AddComponent<Image>().color = CartCard;
                    string have = it.ByJin ? $"{left:0.#} 斤" : $"×{left:0}";
                    Ui.Size(Ui.Label(card, $"  {it.Name} {have}<size=13><color=#A8B0BA>　{it.Where} · 每{(it.ByJin ? "斤" : "件")} {it.UnitKg:0.#} 公斤</color></size>", 15), -1, -1, 1);
                    Ui.Size(Ui.Button(card, "装 1", () => { S.AddToTruck(it.Key, 1); w.Rebuild(); }, 14, Ui.ButtonBg, destWhy == null), 52);
                    Ui.Size(Ui.Button(card, "全装", () => { S.AddToTruck(it.Key, left); w.Rebuild(); }, 14, Ui.ButtonHot, destWhy == null), 56);
                    var drag = card.gameObject.AddComponent<CartDragItem>();
                    drag.Dropped = (pos, cam) =>
                    {
                        if (!RectTransformUtility.RectangleContainsScreenPoint(loadCol, pos, cam)) return;
                        S.AddToTruck(it.Key, 1);
                        w.Rebuild();
                    };
                }

                int cars = S.TruckCars;
                Line(loadList, S.TruckLoad.Count == 0 ? "装货单（拖过来或点“装”）"
                    : $"装货单　{S.TruckKg:0.#} 公斤 → {(cars < 0 ? "有一件一车装不下" : cars == 0 ? "装上刚叫的车" : $"{cars} 辆面包车")}", 17, Ui.Accent);
                foreach (CartLine l in S.TruckLoad.ToList())
                {
                    CartLine line = l;
                    CartLineCard(w, loadList, line, line.Amount, line.Kg, hereCol, () => S.RemoveFromTruck(line.Key, 1),
                        ("−", () => S.RemoveFromTruck(line.Key, 1)), ("×", () => S.RemoveFromTruck(line.Key, line.Amount)));
                }

                RectTransform go = Ui.Row(w.Content, 48, 8);
                Ui.Size(Ui.Button(go, "清空装货单", () => { S.ClearTruckLoad(); w.Rebuild(); }, 16, Ui.ButtonBg), 140);
                string why = S.TruckBlocker;
                var send = Ui.Button(go, why ?? $"出发（运费 {S.TruckFreight}）", () =>
                {
                    if (S.DispatchTruck()) w.Close();
                    else w.Rebuild();
                }, why == null ? 18 : 15, why == null ? Ui.ButtonHot : Ui.ButtonBg, why == null);
                send.name = "TruckDispatch";
                Ui.Size(send, -1, -1, 1);
                Ui.Size(Ui.Button(go, "挂电话", w.Close, 16, Ui.ButtonBg), 110);
            }, true, true, () => S.ClearTruckLoad());
        }

        // ---------- 在店里退租 ----------

        private void ConfirmCloseShop(OwnedShop shop)
        {
            string title = S.ShopTitle(shop);
            OpenWindow("退租 · " + title, 580, 340, confirm =>
            {
                Line(confirm.Content, $"{title}（{shop.Place.Name}）· 月租 {shop.Lease.Rent}", 20, Ui.Accent);
                Para(confirm.Content, $"退租后押金 {shop.Lease.Deposit} 退回，店里摆的家具收回“我的”，这家店的员工辞退，你走到门口。" +
                    (shop == S.MainShop ? "这是主店，退掉后下一家店当主店。" : ""), 17);
                var row = Ui.Row(confirm.Content, 46, 10);
                Ui.Size(Ui.Button(row, "再想想", confirm.Close, 18), -1, 46, 1);
                Ui.Size(Ui.Button(row, "确认退租", () =>
                {
                    if (S.CloseShopBlocker(shop) != null)
                    {
                        confirm.Close();
                        Toast(S.CloseShopBlocker(shop));
                        return;
                    }
                    CloseAllWindows();
                    CloseDock();
                    if (!S.CloseShop(shop.Place.Id) && S.InPlayerShop) OpenDock();
                }, 18, Ui.ButtonHot), -1, 46, 1);
            });
        }
    }
}
