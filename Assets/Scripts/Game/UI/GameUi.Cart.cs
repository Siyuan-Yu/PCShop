using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.World;
using PCShopSell.Game.Runtime;
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

    /// <summary>按下立即提交，长按连续提交；窗口重建后按逻辑键继续同一操作。</summary>
    public sealed class HoldRepeat : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler
    {
        public int Max = 1;
        public Window Owner;
        public string Key;
        public Func<int, string> Label;
        public Action<int> Commit;
        private Button _button;
        private int _count;
        private static HoldRepeat _source;
        private static Window _owner;
        private static string _key;
        private static bool _pressed;
        private static float _held, _next;
        private static int _steps, _releasedFrame = -10;
        public int PendingCount => _count;

        public void OnPointerDown(PointerEventData e)
        {
            _button = GetComponent<Button>();
            if (e.button != PointerEventData.InputButton.Left || Max <= 0 || (_button != null && !_button.interactable)) return;
            _source=this;_owner=Owner;_key=Key;_pressed=true;
            _held=0;_next=.4f;_steps=0;
            SoundFx.Play(Sfx.Click);
            ApplyStep();
        }
        private void ApplyStep()
        {
            _button=GetComponent<Button>();
            if(Max<=0 || (_button!=null&&!_button.interactable)){Stop();return;}
            _count=++_steps;
            string label=Label?.Invoke(1);
            var text=GetComponentInChildren<Text>();
            if(text!=null&&label!=null)text.text=label;
            Commit?.Invoke(1);
        }
        private static void Stop()
        {
            _pressed=false;_releasedFrame=Time.frameCount;
            if(_source!=null)_source._count=0;
            _source=null;_owner=null;_key=null;
        }
        public static void AdvanceActiveHold(float seconds)
        {
            if(!_pressed)return;
            if(!Input.GetMouseButton(0) || (_owner!=null&&(_owner.Closed||_owner.Root==null))){Stop();return;}
            Advance(seconds);
        }
        internal static void CancelActiveHold()
        {
            if (_pressed) Stop();
        }
        private static void Advance(float seconds)
        {
            _held+=Mathf.Max(0,seconds);
            if(_held<_next)return;
            // Rebuilds replace the pressed object. Locate its current equivalent, never
            // reuse callbacks that captured the previous quantity or inventory row.
            HoldRepeat target=null;
            if(_owner!=null)
            {
                if(_owner.RebuildPending)return;
                if (_source != null && _source.isActiveAndEnabled) target = _source;
                else
                    foreach(var hold in _owner.Root.GetComponentsInChildren<HoldRepeat>())
                        if(hold.Owner==_owner&&hold.Key==_key){target=hold;break;}
            }
            else target=_source;
            if(target==null){Stop();return;}
            _source=target;
            target.ApplyStep();
            _next=_held+Mathf.Max(.04f,.16f-_steps*.012f);
        }
        public void AdvanceHold(float seconds) { if(_source==this)Advance(seconds); }
        public void OnPointerUp(PointerEventData e)
        {
            if(e.button==PointerEventData.InputButton.Left&&_pressed)Stop();
        }
        public void ClickOnce()
        {
            if(_pressed||Time.frameCount<=_releasedFrame+1)return;
            _button=GetComponent<Button>();
            if (Max <= 0 || (_button != null && !_button.interactable)) return;
            SoundFx.Play(Sfx.Click);
            Commit?.Invoke(1);
        }
        public void OnBeginDrag(PointerEventData e) { }
        public void OnDrag(PointerEventData e) { }
    }

    public sealed partial class GameUi
    {
        /// <summary>“点一下 1 件、按住多件”的按钮（最多 max）。</summary>
        private void HoldButton(Window w, Transform parent, string text, Color color, double max, string unit, Func<double, bool> commit, float width, bool enabled = true)
        {
            var b = Ui.Button(parent, text, null, 15, color, enabled && max > 1e-9);
            b.name = "Hold_" + text;
            Ui.Size(b, width, 34);
            var hold = b.gameObject.AddComponent<HoldRepeat>();
            hold.Owner=w;hold.Key=parent.name+"|"+b.name;
            hold.Max = Math.Max(1, (int)Math.Ceiling(max - 1e-9));
            hold.Label = n => n <= 1 ? text : $"{text} ×{n}";
            hold.Commit = n =>
            {
                commit(Math.Min(n, max));
                w.RequestRebuild();
            };
            b.onClick.AddListener(hold.ClickOnce);
            b.Tip($"点一下：1 {unit}；长按连续调整并立即应用，无需输入数量。");
        }

        private static readonly Color CartColumn = Ui.FlatBand;
        private static readonly Color CartCard = Ui.FlatWell;

        /// <summary>商品行右边的“加入购物车”按钮；showCount 时前面写购物车里已经有几件。</summary>
        private void CartAddButton(Window w, Transform row, string key, Func<bool> add, string label, float width, bool enabled = true, bool showCount = true,double? maxAmount=null)
        {
            ShopQuantity(w, row, key, add, enabled,maxAmount);
        }

        /// <summary>
        /// 购物车和结账：左栏“装进背包”，右栏“叫车送”。新加商品优先装背包，超出承重的部分自动叫车；
        /// 两栏可手动调整；右栏的一起叫车，按总重算几辆车，运费和货款一起结。
        /// </summary>
        private void CartPanel(Window w, Transform parent, bool defaultToShop)
        {
            RectTransform head = Ui.Row(parent, 34, 8);
            int kinds = S.Cart.Count;
            Ui.Size(Ui.Label(head, kinds == 0 ? "<b>购物车</b>　还是空的，在商品旁选择数量"
                : $"<b>购物车</b>　{kinds} 样 · 共 {S.CartKg:0.##} 千克", 18, TextAnchor.MiddleLeft, Ui.Accent), -1, -1, 1);
            if (kinds == 0) return;
            Ui.Size(Ui.Button(head, "清空", () => { S.ClearCart(); w.Rebuild(); }, 15, Ui.ButtonBg), 70);

            foreach(var line in S.Cart.Where(l=>l.Registered).ToArray())
            {
                var item=line;var asset=Ui.Row(parent,48,8);
                ItemPortrait(asset, item.Key, 42);
                Ui.Size(ProductText(asset,item.Name+" ×"+item.Amount+" · "+item.Price+"\n<size=14>结账后登记为已购资产，不占背包或货车空间</size>",16),-1,-1,1);
                Ui.Size(Ui.Button(asset,"移除",()=>{S.RemoveFromCart(item.Key,item.Amount);w.Rebuild();},15),64,38);
            }
            var quote = S.QuoteCart(defaultToShop);
            var (fee, minutes, cars) = (quote.fee, quote.minutes, quote.cars);
            double truckKg = S.CartTruckKg;
            bool physical=S.Cart.Any(l=>!l.Registered);
            if(physical)
            {
            RectTransform cols = Ui.Row(parent, 190, 10);
            RectTransform bagCol = CartColumnBox(cols, out RectTransform bagList);bagCol.name="CartBag";
            RectTransform truckCol = CartColumnBox(cols, out RectTransform truckList);truckCol.name="CartTruck";
            Line(truckList, truckKg > 1e-9
                ? $"叫车送　{truckKg:0.##} 千克 → {(cars < 0 ? "有一件一车装不下" : cars == 0 ? "装上刚叫的车" : $"{cars} 辆面包车")}"
                : "叫车送　（没有）", 16, Ui.Accent);
            double bagKg = S.CartBagKg, free = S.FreeKg;
            bool over = bagKg > free + 1e-9;
            RectTransform bagHead = Ui.Row(bagList, 24, 8);
            Ui.Size(Ui.Label(bagHead, $"装进背包　{bagKg:0.##} / {free:0.##} 千克", 16, TextAnchor.MiddleLeft, over ? Ui.Warn : Ui.Accent), -1, -1, 1);
            Ui.Bar(bagHead, free <= 0 ? (bagKg > 0 ? 1 : 0) : (float)(bagKg / free), over ? Ui.Warn : Ui.Good, 120, 10);

            foreach (CartLine l in S.Cart.Where(line=>!line.Registered).ToList())
            {
                CartLine line = l;
                if (line.ByTruck > 1e-9)
                    CartLineCard(w, truckList, line, line.ByTruck, line.TruckKg, bagCol, () => S.CartWholeRowToBag(line.Key),
                        new HoldAction("放进背包", Ui.ButtonBg, line.ByTruck, n => S.CartToBag(line.Key, n)),
                        new HoldAction("少拿一件", Ui.ButtonBg, line.ByTruck, n => S.RemoveFromCart(line.Key, n)));
                if (line.InBag > 1e-9)
                    CartLineCard(w, bagList, line, line.InBag, line.BagKg, truckCol, () => S.CartToTruck(line.Key, line.InBag),
                        new HoldAction("改叫车", Ui.ButtonBg, line.InBag, n => S.CartToTruck(line.Key, n)),
                        new HoldAction("少拿一件", Ui.ButtonBg, line.InBag, n => S.CartToTruck(line.Key, n) && S.RemoveFromCart(line.Key, n)));
            }
            if (bagKg <= 1e-9) Para(bagList, "新购商品默认优先放背包；装不下的自动放到右侧叫车。两边可按需调整。", 14, Ui.Dim);

            RectTransform destRow = Ui.Row(parent, 40, 8);
            if (truckKg > 1e-9)
            {
                Ui.Size(Ui.Label(destRow, "送到", 16, TextAnchor.MiddleLeft, Ui.Dim), 40);
                Ui.Size(CourierDestinationButton(destRow, w, quote.destination, 15, quote.residence), 300);
                Ui.Size(Ui.Label(destRow, $"约 {minutes} 分钟到，每辆面包车最多装 {GameSession.CourierCapacityKg:0} 千克", 15, TextAnchor.MiddleLeft, Ui.Dim), -1, -1, 1);
            }
            else Ui.Size(Ui.Label(destRow, "全部自己背走，不叫车。", 15, TextAnchor.MiddleLeft, Ui.Dim), -1, -1, 1);

            }
            // 钱都写在这一行：货款 + 运费 = 合计
            Money goods = quote.goods;
            Money freight = cars > 0 ? fee * cars : Money.Zero;
            Money total = goods + freight;
            string freightText = truckKg <= 1e-9 ? "运费 ¥0（不叫车）"
                : cars > 0 ? $"运费 {freight}（{cars} 辆 × {fee}）"
                : cars == 0 ? "运费 ¥0（装上刚叫的车）" : "运费：有一件一车装不下";
            Image bill = Ui.Panel(parent, "CartBill", CartColumn);
            Ui.Size(bill, -1, 44);
            Text billText = Ui.Label(bill.transform, $"货款 {goods}　＋　{freightText}　＝　<b><color=#80551F>合计 {total}</color></b>", 19, TextAnchor.MiddleCenter);
            billText.rectTransform.Fill();

            RectTransform pay = Ui.Row(parent, 46, 8);
            if(physical)Ui.Size(Ui.Button(pay, "背得动的都放背包", () => { S.CartFillBag(); w.Rebuild(); }, 15, Ui.ButtonBg), 170);
            if(physical)Ui.Size(Ui.Button(pay, "全部叫车", () => { S.CartAllToTruck(); w.Rebuild(); }, 15, Ui.ButtonBg), 110);
            string why = quote.blocker;
            var checkout = Ui.Button(pay, why ?? "结账（付合计）", () => CheckoutCart(w, defaultToShop),
                why == null ? 18 : 14, why == null ? Ui.ButtonHot : Ui.ButtonBg, why == null);
            checkout.name = "CartCheckout";
            Ui.Size(checkout, -1, -1, 1);
        }

        private void CheckoutCart(Window window, bool defaultToShop)
        {
            if (!S.Checkout(defaultToShop))
            {
                window.Rebuild();
                return;
            }
            SoundFx.Play(Sfx.Purchase);
            // The next lesson points at the HUD, so release the furniture catalog after the opening purchase.
            if (QuestPopups && S.AtMall && S.PendingQuestCompletion()?.Id == "mall"
                && S.OpeningRenovationStep == OpeningRenovationLesson.Travel)
                window.Close();
            else window.Rebuild();
            UpdateQuestTracker();
        }

        private static RectTransform CartColumnBox(Transform row, out RectTransform list)
        {
            Image box = Ui.FlatPanel(row, "CartColumn", CartColumn);
            Ui.Size(box, -1, 190, 1);
            list = Ui.Scroll(box.transform, -1);
            ((RectTransform)list.parent.parent).Fill();
            return box.rectTransform;
        }

        /// <summary>卡片后面的按钮：点一下做 1 件，按住一件件连续调整（最多 Max）。</summary>
        private readonly struct HoldAction
        {
            public readonly string Text;
            public readonly Color Color;
            public readonly double Max;
            public readonly Func<double, bool> Commit;

            public HoldAction(string text, Color color, double max, Func<double, bool> commit)
            {
                Text = text;
                Color = color;
                Max = max;
                Commit = commit;
            }
        }

        /// <summary>一张能拖的卡片：名字、数量、重量，后面是“点一下 1 件、按住多件”的按钮；拖到 target 栏松手执行 onDrop。</summary>
        private void CartLineCard(Window w, RectTransform list, CartLine l, double amount, double kg, RectTransform target, Func<bool> onDrop,
            params HoldAction[] buttons)
        {
            RectTransform card = Ui.Row(list, 68, 4);card.name="CartLine_"+l.Key+"_to_"+target.name;
            Image bg = card.gameObject.AddComponent<Image>();
            bg.color = CartCard;
            ItemPortrait(card, l.Key, 44);
            string amountText = l.ByWeight ? $"{amount:0.##} 千克" : l.Key.StartsWith("ing:") ? $"{kg:0.##} 千克" : $"×{amount:0}";
            string price = l.UnitPrice != null ? $"　{l.UnitPrice().Scale(amount)}" : "";
            Ui.Size(ProductText(card, ProductLabel($"  {l.Name} {amountText}", l.UnitKg, $"本批 {kg:0.###} 千克{price}", l.ByWeight ? "每千克" : "每件"), 15), -1, -1, 1);
            foreach (HoldAction a in buttons)
                HoldButton(w, card, a.Text, a.Color, a.Max, l.ByWeight ? "千克" : "件", a.Commit, a.Text.Length > 3 ? 104 : 76);
            var drag = card.gameObject.AddComponent<CartDragItem>();
            drag.Dropped = (pos, cam) =>
            {
                if (!RectTransformUtility.RectangleContainsScreenPoint(target, pos, cam)) return;
                onDrop();
                w.Rebuild();
            };
        }

        private Button CourierDestinationButton(Transform parent, Window owner, OwnedShop destination, int fontSize, Place residence)
        {
            string name = residence == null ? S.CourierDestName(destination) : S.PlaceTitle(residence);
            var button = Ui.Button(parent, name + "  ▼", () =>
            {
                if (residence == null) OpenCourierDestMenu(owner, destination);
                else OpenTruckDestMenu(owner, residence, forCart: true);
            }, fontSize, Ui.FlatRow);
            button.name = "CourierDestination";
            return button;
        }

        /// <summary>购物配送地址：家里和各家门店。</summary>
        private void OpenCourierDestMenu(Window owner, OwnedShop current)
        {
            int n = 1 + S.MyShopCount;
            OpenWindow("送到哪", 600, 150 + n * 72, w =>
            {
                void Option(OwnedShop shop)
                {
                    (Money fee, int minutes) = S.CourierQuote(shop);
                    bool chosen = shop == current;
                    string sub = $"每辆运费 {fee}，约 {minutes} 分钟到";
                    Ui.Size(Ui.Button(w.Content, $"{S.CourierDestName(shop)}{(chosen ? "  ✓" : "")}\n<size=13>{sub}</size>", () =>
                    {
                        S.SetCourierDest(shop);
                        w.Close();
                        owner?.Rebuild();
                    }, 18, Ui.SelectedButton(chosen)), -1, 64);
                }
                Option(null);
                foreach (OwnedShop s in S.MyShops) Option(s);
                if (!S.HasShop) Line(w.Content, "还没有店铺，去房产中介租一间才能送店里。", 15, Ui.Dim);
                Ui.Size(Ui.Button(w.Content, "取消", w.Close, 18), -1, 44);
            });
        }

        // ---------- 打电话叫货车 ----------

        private void OpenTruckDestMenu(Window owner, Place selected, bool forCart = false)
        {
            var destinations = S.TruckDestinations.ToArray();
            var residences = destinations.Where(place => place.Kind == PlaceKind.Home).ToArray();
            var shops = destinations.Where(place => place.Kind != PlaceKind.Home).ToArray();
            int rows = forCart ? residences.Length : Math.Max(residences.Length, shops.Length);
            OpenWindow("送到哪", forCart ? 650 : 980, Math.Min(850, 138 + rows * 64), w =>
            {
                var columns = Ui.Row(w.Content, -1, 14);
                columns.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
                BuildGroup("住宅", residences);
                if (!forCart && shops.Length > 0) BuildGroup("店铺", shops);

                void BuildGroup(string title, Place[] addresses)
                {
                    var column = Ui.Size(Ui.Rect(columns, title), 0, -1, 1);
                    Ui.VList(column, 6, 0);
                    Line(column, title, 18, Ui.Accent);
                    foreach (var address in addresses)
                    {
                        string blocker = forCart ? S.CartResidenceBlocker(address) : S.TruckDestinationBlocker(address);
                        var (fee, minutes) = S.TruckQuote(address);
                        string note = address == S.CurrentPlace ? "你就在这里" : blocker ?? $"每辆 {fee} · 约 {minutes} 分钟";
                        bool chosen = address == selected;
                        Ui.Size(Ui.Button(column, S.PlaceTitle(address) + (chosen ? "  ✓" : "") + "\n<size=14>" + note + "</size>", () =>
                        {
                            bool chosenAddress = forCart ? S.SelectCartResidence(address) : S.SelectTruckDestination(address);
                            if (!chosenAddress) { w.Rebuild(); return; }
                            w.Close();
                            owner.Rebuild();
                        }, 17, Ui.SelectedButton(chosen), blocker == null), -1, 58);
                    }
                }
            });
        }

        /// <summary>
        /// 打电话叫货车：左边是这里能交给司机的东西，点“装车”（按住装多件）或拖到右边装货单，选好送到哪，点出发一起付运费。
        /// </summary>
        public void OpenTruckWindow()
        {
            string category = null;
            OpenWindow("叫货车 · " + GameSession.TruckDriverName, 1180, 860, w =>
            {
                Para(w.Content, $"每辆限重 {GameSession.CourierCapacityKg:0} 千克，已摆放的家具需先收纳。", 15, Ui.Dim);
                Place dest = S.TruckDestination;
                RectTransform destRow = Ui.Row(w.Content, 42, 8);
                Ui.Size(Ui.Label(destRow, "送到", 17, TextAnchor.MiddleLeft, Ui.Dim), 44);
                Ui.Size(Ui.Button(destRow, S.PlaceTitle(dest) + "  ▼", () => OpenTruckDestMenu(w, dest), 16, Ui.FlatRow), 320)
                    .name = "CourierDestination";
                string destWhy = S.TruckDestBlocker;
                (Money fee, int minutes) = S.TruckQuote(dest);
                Ui.Size(Ui.Label(destRow, destWhy ?? $"每辆 {fee}，约 {minutes} 分钟到", 15, TextAnchor.MiddleLeft, destWhy != null ? Ui.Warn : Ui.Dim), -1, -1, 1);

                RectTransform cols = Ui.Row(w.Content, 0, 10);
                cols.GetComponent<LayoutElement>().flexibleHeight = 1;
                RectTransform hereCol = CartColumnBox(cols, out RectTransform hereList);
                RectTransform loadCol = CartColumnBox(cols, out RectTransform loadList);
                Ui.Size(hereCol, -1, 0, 1);
                Ui.Size(loadCol, -1, 0, 1);

                const float headerHeight = 36;
                var hereScroll = hereList.GetComponentInParent<ScrollRect>();
                ((RectTransform)hereScroll.transform).Anchor(0, 0, 1, 1, top: headerHeight + 10);
                var filterRow = Ui.Row(hereCol, headerHeight, 8)
                    .Anchor(0, 1, 1, 1, 6, -headerHeight - 6, 6, 6);
                Ui.Size(Ui.Label(filterRow, "这里能搬的", 17, TextAnchor.MiddleLeft, Ui.Accent), -1, -1, 1);
                Ui.Size(Ui.Label(filterRow, "物品分类", 15, TextAnchor.MiddleLeft, Ui.Dim), 68);

                var items = S.MovableHere();
                var categories = items.Select(item => item.Category).Distinct().OrderBy(value => value)
                    .Prepend("全部物品").ToArray();
                int selected = Array.IndexOf(categories, category);
                if (selected < 0) category = null;
                var filter = Ui.Choice(filterRow, categories, Math.Max(0, selected), index =>
                {
                    category = index == 0 ? null : categories[index];
                    Ui.Clear(hereList);
                    BuildTruckAvailableItems(w, hereList, loadCol, items, category, destWhy == null);
                    LayoutRebuilder.ForceRebuildLayoutImmediate(hereList);
                    hereScroll.StopMovement();
                    hereScroll.verticalNormalizedPosition = 1;
                });
                filter.name = "TruckItemCategory";
                filter.interactable = categories.Length > 1;
                Ui.Size(filter, 200, headerHeight);
                BuildTruckAvailableItems(w, hereList, loadCol, items, category, destWhy == null);

                int cars = S.TruckCars;
                Line(loadList, S.TruckLoad.Count == 0 ? "装货单（点左边“装车”，按住装多件；也可以拖过来）"
                    : $"装货单　{S.TruckKg:0.##} 千克 → {(cars < 0 ? "有一件一车装不下" : cars == 0 ? "装上刚叫的车" : $"{cars} 辆面包车")}", 17, Ui.Accent);
                foreach (CartLine l in S.TruckLoad.ToList())
                {
                    CartLine line = l;
                    CartLineCard(w, loadList, line, line.Amount, line.Kg, hereCol, () => S.RemoveFromTruck(line.Key, line.Amount),
                        new HoldAction("拿下来", Ui.ButtonBg, line.Amount, n => S.RemoveFromTruck(line.Key, n)));
                }

                RectTransform go = Ui.Row(w.Content, 48, 8);
                Ui.Size(Ui.Button(go, "清空装货单", () => { S.ClearTruckLoad(); w.Rebuild(); }, 16, Ui.ButtonBg), 140);
                string why = S.TruckBlocker;
                var send = Ui.Button(go, why ?? $"出发（运费 {S.TruckFreight}）", () =>
                {
                    if (S.DispatchTruck())
                    {
                        SoundFx.Play(Sfx.Purchase);
                        w.Close();
                    }
                    else w.Rebuild();
                }, why == null ? 18 : 15, why == null ? Ui.ButtonHot : Ui.ButtonBg, why == null);
                send.name = "TruckDispatch";
                Ui.Size(send, -1, -1, 1);

            }, true, true, () => S.ClearTruckLoad(), scrollContent: false);
        }

        private void BuildTruckAvailableItems(Window w, RectTransform list, RectTransform loadCol,
            List<MovableItem> items, string category, bool canLoad)
        {
            bool any = false;
            foreach (var item in items)
            {
                if (category != null && item.Category != category) continue;
                double left = item.Have - S.TruckAmount(item.Key);
                if (left <= 1e-9) continue;
                any = true;
                RectTransform card = Ui.Row(list, 38, 4);
                card.name = "TruckLoad_" + item.Key;
                card.gameObject.AddComponent<Image>().color = CartCard;
                ItemPortrait(card, item.Key, 34);
                string have = item.ByWeight ? $"{left:0.##} 千克" : $"×{left:0}";
                Ui.Size(Ui.Label(card, $"  {item.Name} {have}<size=13><color=#655E52>　{item.Where} · 每{(item.ByWeight ? "千克" : "件")} {item.UnitKg:0.##} 千克</color></size>", 15), -1, -1, 1);
                HoldButton(w, card, "装车", Ui.ButtonBg, left, item.ByWeight ? "千克" : "件", n => S.AddToTruck(item.Key, n), 90, canLoad);
                var drag = card.gameObject.AddComponent<CartDragItem>();
                drag.Dropped = (pos, cam) =>
                {
                    if (!RectTransformUtility.RectangleContainsScreenPoint(loadCol, pos, cam)) return;
                    S.AddToTruck(item.Key, left);
                    w.Rebuild();
                };
            }

            if (!any) Para(list, category == null ? "这里没有剩余可搬的物品。" : "这一类没有剩余可搬的物品。", 15, Ui.Dim);
        }

        // ---------- 店内 / 中介退租 ----------

        private void ConfirmCloseShop(OwnedShop shop)
        {
            string title = S.ShopTitle(shop);
            string action = shop.OwnsProperty ? "结束自营" : "退租";
            bool finalReview = false;
            OpenWindow(action + " · " + title, 660, 460, confirm =>
            {
                Line(confirm.Content, finalReview ? $"再次确认{action} · {title}" : $"{title} · {S.City.StreetName(shop.Place.StreetId)}", 20, Ui.Accent);
                Para(confirm.Content, shop.OwnsProperty ? "保留商铺产权，铺面转为空置。" : $"已交押金 {shop.Lease.Deposit} 不退，铺面重新挂牌。", 17);
                Para(confirm.Content, Ui.Emphasize("店内货物、家具和装修会永久清空。", Ui.Warn), 17);
                if (!finalReview)
                {
                    Para(confirm.Content, "员工转为待安排。已付款的在途货物改送家中，还没采购的部分取消。", 17);
                    if (shop.Sim.Orders.Count > 0)
                        Para(confirm.Content, "未完成的装机单会取消，企业订单按原规则结算履约保证金。", 17, Ui.Warn);
                }
                string blocker = S.CloseShopBlocker(shop);
                if (blocker != null) Para(confirm.Content, "暂不能办理：" + blocker + "。", 17, Ui.Warn);
                var row = Ui.Row(confirm.Content, 46, 10);
                Ui.Size(Ui.Button(row, finalReview ? "返回上一步" : "再想想", () =>
                {
                    if (!finalReview) { confirm.Close(); return; }
                    finalReview = false;
                    confirm.Rebuild();
                }, 18), -1, 46, 1);
                var submit = Ui.Size(Ui.Button(row, finalReview ? "清空并" + action : "继续", () =>
                {
                    if (S.CloseShopBlocker(shop) is string reason)
                    {
                        Toast(reason);
                        confirm.Rebuild();
                        return;
                    }
                    if (!finalReview)
                    {
                        finalReview = true;
                        confirm.Rebuild();
                        return;
                    }
                    CloseAllWindows();
                    CloseDock();
                    if (!S.CloseShop(shop.Place.Id) && S.InPlayerShop) OpenDock();
                }, 18, Ui.Warn, blocker == null), -1, 46, 1);
                submit.name = finalReview ? "ConfirmCloseShop" : "ReviewCloseShop";
            }, fitContent: true);
        }
    }
}
