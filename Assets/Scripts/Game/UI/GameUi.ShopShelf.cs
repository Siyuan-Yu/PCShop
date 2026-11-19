using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Home;
using PCShopSell.Core.Shop;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    /// <summary>货架与展示台的陈列、售价和店内取放。</summary>
    public sealed partial class GameUi
    {
        private static Color ShelfRailColor => Color.Lerp(Ui.FlatCardEdge, Ui.ButtonHot, .35f);

        private static string ShelfMarketMovement(double? change)
        {
            string movement = change.HasValue ? PriceMovement(change.Value) : "—";
            string text = change.HasValue && movement == "—" ? "行情持平" : $"行情 {movement}";
            Color color = change.HasValue ? PriceMovementColor(change.Value) : Ui.Dim;
            return $"<size=13><color=#{ColorUtility.ToHtmlStringRGB(color)}> · {text}</color></size>";
        }

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

        // ---------- 货架 ----------

        /// <summary>走到货架前按 F，可查看陈列商品、调价或更换品类。</summary>
        public void OpenShelfWindow(int layoutIndex)
        {
            int page = 0;
            string previousKind = null;
            OpenCompactWindow("货架", 1240, w =>
            {
                if (layoutIndex < 0 || layoutIndex >= S.ShopLayout.Count) { w.Close(); return; }
                ShopSimulation shop = S.PlayerShop;
                ShelfKind kind = S.ShelfKindAt(layoutIndex);
                if (previousKind != kind?.Id) { previousKind = kind?.Id; page = 0; }
                void ChangePage(int value) { page = value; w.Rebuild(); }

                RectTransform head = Ui.Row(w.Content, 44, 8);
                FurnitureDef shelf = FurnitureCatalog.Get(S.ShopLayout[layoutIndex].Id);
                var fill = kind == null ? null : shop.ShelfFillAt(layoutIndex, kind) ?? new ShelfFill(kind, shelf.ShelfKg, shelf.ShelfItems);
                string title = kind == null
                    ? $"<b>这个货架还空着</b>\n<size=15>最多 {shelf.ShelfItems} 件 · {shelf.ShelfKg:0} 千克</size>"
                    : $"<b>{kind.Name}</b>\n<size=15>本架 {fill.UsedKg:0.##}/{fill.CapacityKg:0} 千克 · {fill.Total}/{fill.CapacityItems} 件{(fill.Full ? " · 已满" : "")}{(kind.Id == "secondhand" ? "" : "　|　行情较昨日")}</size>";
                Ui.Size(Ui.Label(head, title, 19, TextAnchor.MiddleLeft), 0, -1, 1);
                if (kind != null && kind.Id != "secondhand")
                    Ui.Size(HelpLink(head, "调价说明", "长按加减号可加速。调价时可预览购买意愿，确认后生效；同型号盒装和散件的售价联动。"), 152, 36);
                Ui.Size(Ui.Button(head, kind == null ? "选择摆什么" : "换品类", () => OpenShelfKindChooser(layoutIndex, w), 17, kind == null ? Ui.ButtonHot : Ui.ButtonBg), 138, 36);
                if (kind == null)
                {
                    Line(w.Content, "选好品类后，从后仓自动上架。", 17, Ui.Dim);
                    return;
                }

                if (kind.Id == "secondhand")
                {
                    BuildUsedStock(w.Content, w, true, displayed: fill.Resale.Select(o => o.Id).ToArray(), shelfPage: page, changeShelfPage: ChangePage);
                    return;
                }
                if (kind.IsParts) BuildPartShelfBody(w, kind, fill);
                else BuildGoodsShelfBody(w, kind, fill, page, ChangePage);
            });
        }

        private void OpenCompactWindow(string title, float width, System.Action<Window> build)
        {
            OpenWindow(title, width, 690, w =>
            {
                build(w);
                if (w.Closed) return;
                LayoutRebuilder.ForceRebuildLayoutImmediate(w.Content);
                var panel = (RectTransform)w.Content.parent;
                panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                    Mathf.Max(185, Mathf.Ceil(LayoutUtility.GetPreferredHeight(w.Content) + 54)));
                w.Fitter.Fit();
            }, scrollContent: false);
        }

        private static void BuildCompactPageControls(Transform parent, int page, int pages, System.Action<int> change)
        {
            if (pages <= 1) return;
            var row = Ui.Row(parent, 34, 8);
            Ui.Size(Ui.Button(row, "上一页", () => change(page - 1), 16, interactable: page > 0), 106);
            Ui.Size(Ui.Label(row, $"{page + 1} / {pages}", 16, TextAnchor.MiddleCenter), 0, -1, 1);
            Ui.Size(Ui.Button(row, "下一页", () => change(page + 1), 16, interactable: page + 1 < pages), 106);
        }

        /// <summary>选这个货架摆哪一类货：一个小窗口，点哪类就是哪类。owner 是从货架窗口打开的就一起刷新。</summary>
        public void OpenShelfKindChooser(int layoutIndex, Window owner = null)
        {
            string group=S.ShelfKindAt(layoutIndex)?.BrowseGroup??(S.RetailKind=="computer"?"电脑配件":S.RetailKind=="clothing"?"服装":S.RetailKind=="models"?"模型手办":S.RetailKind==PetShopCatalog.ShopKind?"宠物用品":"零售商品");
            OpenCompactWindow("这个货架摆什么", 1000, w =>
            {
                var shop=S.PlayerShop;var current=S.ShelfKindAt(layoutIndex);
                Line(w.Content,"先选大类，再选具体品类；所有店都能混合陈列，后仓现货会自动摆上来。",15,Ui.Dim);
                var groups=Ui.Row(w.Content,38,6);
                foreach(string name in new[]{"全部","服装","电脑配件","模型手办","宠物用品","零售商品"})
                {
                    string choice=name;
                    var button=Ui.Size(Ui.Button(groups,choice,()=>{group=choice;w.Rebuild();},16,Ui.SelectedButton(group==choice)),-1,38,1);
                    button.name="ShelfGroup_"+choice;
                }
                void Pick(string id)
                {
                    if(!S.SetShelfKind(layoutIndex,id))return;
                    w.Close();owner?.Rebuild();RebuildDock();
                }
                foreach(var section in ShelfKinds.All.Where(k=>group=="全部" || k.BrowseGroup==group).GroupBy(k=>k.BrowseGroup))
                {
                    const int columns = 5;
                    Line(w.Content, section.Key, 16, Ui.Accent);
                    RectTransform row=null;int n=0;
                    foreach(var kind in section)
                    {
                        var option=kind;
                        if(n++ % columns == 0)row=Ui.Row(w.Content, 46, 6);
                        int count=kind.Id=="secondhand"?S.SpecialBusiness(S.CurrentShop.Place.Id).Used.Count(i=>i.Owned&&!i.Sold):kind.IsParts?PartCatalog.InCategory(kind.Category).Sum(p=>shop.Stock.CountAll(p)):kind.GiftIds.Sum(id=>shop.Stock.GoodsCount(id));
                        int shelves=S.ShelvesOfKind(kind.Id);
                        string info=$"在库 {count} 件 · {(shelves==0?"尚未陈列":shelves+" 个货架")}";
                        var button=Ui.Size(Ui.Button(row,kind.Name+(current?.Id==kind.Id?" ✓":"")+"\n<size=13>"+info+"</size>",()=>Pick(option.Id),17,current?.Id==kind.Id?Ui.ButtonHot:Ui.ButtonBg),0,-1,1);
                        button.name="ShelfKind_"+kind.Id;
                    }
                    for (int i = n % columns; i > 0 && i < columns; i++)
                        Ui.Size(Ui.Rect(row, "Pad"), 0, -1, 1);
                }
                var actions=Ui.Row(w.Content,46,8);
                Ui.Size(Ui.Button(actions,"清空这个货架",()=>Pick(null),17,Ui.ButtonBg,current!=null),-1,46,1);
                Ui.Size(Ui.Button(actions,"取消",w.Close,17),160,46);
            });
        }

        private void BuildPartShelfBody(Window w, ShelfKind kind, ShelfFill fill)
        {
            ShopSimulation shop = S.PlayerShop;
            var parts = PartCatalog.InCategory(kind.Category).OrderBy(p => p.Tier).ToArray();
            var today = S.CurrentMarketPriceSnapshot;
            var yesterday = S.PriceDays.FirstOrDefault(day => day.Day == today.Day - 1);
            int group = System.Array.IndexOf(PartCatalog.Categories, kind.Category);
            double? change = group >= 0 ? ComparePrice(today, yesterday, group) : null;
            Transform list = w.Content;
            for (int board = 0; board < 2; board++)
            {
                bool boxed = board == 0;
                var sign = Ui.Size(Ui.FlatPanel(list, "ShelfSection", Ui.FlatBand), -1, 26);
                Ui.Label(sign.transform, boxed ? "整盒" : "散件", 17, TextAnchor.MiddleLeft, Ui.Accent)
                    .rectTransform.Fill(5);
                var stocked = parts.Where(part => fill.PartCount(part, boxed) > 0).ToArray();
                if (stocked.Length == 0)
                    Line(list, "暂无陈列", 16, Ui.Dim);
                foreach (PartDef part in stocked)
                    BuildShelfSlot(w, list, part, boxed, shop, fill.PartCount(part, boxed), change);
            }
        }

        private void BuildShelfSlot(Window w, Transform parent, PartDef part, bool boxed, ShopSimulation shop, int count, double? change)
        {
            var card = Ui.Size(Ui.FlatPanel(parent, "PartShelfSlot", Ui.PanelBg2), -1, 70);
            RectTransform row = Ui.Row(card.transform, 70, 12).Fill();
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 4, 4);

            layout.childForceExpandHeight = false;
            ItemPortrait(row, part.Id, 54).name = "PartArtwork";

            string details = $"{PartCatalog.TierName(part.Tier)} · {(boxed ? "盒装" : "散件")} · 架上 {count} 件 · 每件 {PartUnitKg(part, boxed):0.##} 千克";
            Ui.Size(Ui.Label(row, $"<b>{part.Name}</b>\n<size=15>{details}</size>{ShelfMarketMovement(change)}",
                19, TextAnchor.MiddleLeft), 0, -1, 1);
            var tag = ShelfSaleTag(w, row, part.Id + (boxed ? "_boxed" : "_loose"), shop.PriceOf(part, boxed).Yuan,
                shop.PartPriceRange(part, boxed), value => shop.PartPurchaseIntentAtPrice(part, boxed, value),
                value => shop.SetPartSalePrice(part, boxed, value));
            Ui.Size(tag, 306, 60);
        }
        // ---------- 整机展示台 ----------

        /// <summary>展示台上的整机：三台开着的整机并排，屏幕上是档次和成交价，台面下的牌子写配置、配件齐不齐。</summary>
        public void OpenDisplayWindow()
        {
            int selected=S.DisplayIndexes().Count>0?S.DisplayIndexes()[0]:-1;
            OpenCompactWindow("展示台", 980, w =>
            {
                var indexes=S.DisplayIndexes();
                if(indexes.Count==0){Para(w.Content,"先购买并摆放一张展示台，再选择展示品类。",19);return;}
                if(!indexes.Contains(selected))selected=indexes[0];
                if (indexes.Count > 1)
                {
                    var selector=Ui.Row(w.Content,40,6);
                    int current = indexes.ToList().IndexOf(selected);
                    string name = S.IsComputerDisplay(selected) ? "电脑整机" : ShelfKinds.NameOf(S.ShopLayout[selected].Item);
                    void Select(int index) { selected = indexes[index]; w.Rebuild(); }
                    Ui.Size(Ui.Button(selector, "上一张", () => Select(current - 1), 16, interactable: current > 0), 106);
                    Ui.Size(Ui.Label(selector, $"展示台 {current + 1}/{indexes.Count} · {name}", 17, TextAnchor.MiddleCenter), 0, -1, 1);
                    Ui.Size(Ui.Button(selector, "下一张", () => Select(current + 1), 16, interactable: current + 1 < indexes.Count), 106);
                }
                Ui.Size(Ui.Button(w.Content,"更换这张展示台的品类",()=>OpenDisplayKindPicker(selected,w),18,Ui.ButtonBg),-1,42);
                if(!S.IsComputerDisplay(selected)){BuildGoodsDisplay(w,selected);return;}
                ShopSimulation shop = S.PlayerShop;
                int shown = shop.DisplayPcs.Count;
                Para(w.Content, "用库存配件装好整机摆上展示台（店正中间）：想买整机的顾客看到就直接抱去收银台结账，不用等你装。" +
                                $"展台上的机器越多越显眼，想买整机的顾客也越多。展台没有合适的机器且订单未满时，顾客会下装机单（{shop.Settings.BuildOrderDueDays} 天内交付）。", 15, Ui.Dim);
                Para(w.Content, "整机售价 = 同套配件盒装分卖总价 × 1.20 + 装机手工费。配件调价会同步影响新整机报价。", 15, Ui.Accent);
                Line(w.Content, $"展台上 <b>{shown}/{shop.DisplaySlots}</b> 台　·　想买整机的顾客占 <b>{shop.BuildChance:P0}</b>（展台空着时 {shop.Settings.BuildOrderChance:P0}）", 17);

                Image table = Ui.FlatPanel(w.Content, "Table", Ui.FlatBand);
                Ui.Size(table, -1, 470);
                RectTransform row = Ui.Row(table.transform, 470, 10);
                row.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(10, 10, 10, 10);
                row.Fill();
                for (int tier = 0; tier < 3; tier++) BuildDisplayStand(row, tier, shop);

                RectTransform acts = Ui.Row(w.Content, 46, 10);
                acts.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(10, 10, 0, 0);
                for (int tier = 0; tier < 3; tier++)
                {
                    int t = tier;
                    bool ready = PartCatalog.BuildList(t).All(p => shop.Stock.CountAll(p) > 0);
                    bool canAdd = ready && shown < shop.DisplaySlots;
                    Ui.Size(Ui.Button(acts, "装一台摆上去", () =>
                    {
                        if (S.AssembleDisplayPc(t)) Toast($"{PartCatalog.TierName(t)}整机摆上展示台了");
                        RebuildWindows();
                    }, 17, canAdd ? Ui.ButtonHot : Ui.ButtonBg, canAdd), 196);
                    bool has = shop.DisplayCount(t) > 0;
                    Ui.Size(Ui.Button(acts, "拆回配件", () =>
                    {
                        S.DisassembleDisplayPc(t);
                        RebuildWindows();
                    }, 15, Ui.ButtonBg, has), 92);
                }

                int orders = shop.Orders.Count;
                Line(w.Content, orders > 0 ? $"现在有 {orders} 个订单等你处理。" : "现在没有订单。", 17, orders > 0 ? Ui.Warn : Ui.Dim);
                RectTransform bar = Ui.Row(w.Content, 46);
                if (orders > 0)
                    Ui.Size(Ui.Button(bar, "去处理订单", () =>
                    {
                        w.Close();
                        OpenDock();
                        SelectTab(ShopTab.Orders);
                    }, 19, Ui.ButtonBg), 300);
                Ui.Size(Ui.Button(bar, "关闭", w.Close, 19), 200);
            });
        }

        private void OpenDisplayKindPicker(int layoutIndex,Window parent)
        {
            OpenCompactWindow("选择展示品类", 940, w =>
            {
                void Option(Transform row, string id, string name)
                {
                    Ui.Size(Ui.Button(row, name, () =>
                    {
                        if (!S.SetDisplayKind(layoutIndex, id)) return;
                        w.Close();
                        parent.Rebuild();
                        RebuildDock();
                    }, 17), 0, 42, 1);
                }
                var options = new[] { (Id: "computer", Name: "电脑整机") }
                    .Concat(ShelfKinds.All.Select(kind => (kind.Id, kind.Name))).ToArray();
                const int columns = 4;
                for (int start = 0; start < options.Length; start += columns)
                {
                    var row = Ui.Row(w.Content, 42, 6);
                    for (int i = start; i < start + columns; i++)
                        if (i < options.Length) Option(row, options[i].Id, options[i].Name);
                        else Ui.Size(Ui.Rect(row, "Pad"), 0, -1, 1);
                }
            });
        }
        private void BuildGoodsDisplay(Window w,int index)
        {
            var kind=S.ShelfKindAt(index);if(kind==null){Para(w.Content,"请选择展示品类。",18);return;}
            int limit=S.DisplayLimitAt(index);
            Para(w.Content,"按所选品类从后仓自动补货，顾客可直接购买。更换品类时商品仍留在后仓。",17,Ui.Dim);
            var quantity=Ui.Row(w.Content,44,8);
            Ui.Size(Ui.Label(quantity,"这张展示台摆放数量",18),230);
            QuantityStepper(w,quantity,"display_"+index,limit,1,6,1,
                value=>S.SetDisplayLimit(index,(int)value),unit:"件");
            var fill=S.DisplayFill(index);
            Line(w.Content,kind.Name+" · 可陈列 "+fill.Total+"/"+limit+" 件",21,Ui.Accent);
            Transform list = w.Content;
            if(fill.Total==0)Para(list,"后仓暂时没有这一品类的商品，可先批发进货，再到“库存”中上架。",18,Ui.Dim);
            foreach(var item in fill.Goods)
            {
                var product=Gifts.Get(item.Key);if(product==null)continue;
                var row=Ui.Row(list,52,10);
                ItemPortrait(row, product.Id, 44);
                Ui.Size(ProductText(row,ProductLabel(product.Name,product.WeightKg,"陈列 "+item.Value+" 件"),18),-1,-1,1);
                Ui.Size(Ui.Label(row,S.PlayerShop.GoodsPriceOf(item.Key).ToString(),20,TextAnchor.MiddleLeft,Ui.Good),100);
            }
            foreach(var line in fill.Parts)
            {
                var row=Ui.Row(list,52,10);
                ItemPortrait(row, line.Part.Id, 44);
                Ui.Size(ProductText(row,ProductLabel(line.Part.Name,PartUnitKg(line.Part,line.Boxed),"陈列 "+line.Count+" 件"),18),-1,-1,1);
                Ui.Size(Ui.Label(row,S.PlayerShop.PriceOf(line.Part,line.Boxed).ToString(),20,TextAnchor.MiddleLeft,Ui.Good),100);
            }
            Ui.Size(Ui.Button(w.Content,"从背包或后仓上架商品",OpenRetailListing,18),-1,42);
        }

        private void BuildDisplayStand(Transform row, int tier, ShopSimulation shop)
        {
            var parts = PartCatalog.BuildList(tier);
            var missing = parts.Where(p => shop.Stock.CountAll(p) == 0).ToList();
            bool ready = missing.Count == 0;
            Image col = Ui.FlatPanel(row, "Stand", Ui.PanelBg);
            Ui.Size(col, 294, 450);
            RectTransform rt = col.rectTransform;

            Image screen = Ui.FlatPanel(rt, "Screen", Ui.FlatWell, Ui.FlatWellEdge);
            screen.rectTransform.Anchor(.04f, .48f, .96f, .98f);
            Text sn = Ui.Label(screen.transform, $"<b>{PartCatalog.TierName(tier)}整机</b>", 23, TextAnchor.MiddleCenter);
            FillItemPortrait(screen.transform, "pcbuild:" + tier).Anchor(.04f, .25f, .45f, .92f);
            sn.rectTransform.Anchor(.46f, .55f, 1, 1);
            Text sp = Ui.Label(screen.transform, $"<b>{shop.BuildPrice(tier)}</b>", 25, TextAnchor.MiddleCenter, Ui.Accent);
            sp.rectTransform.Anchor(.46f, .25f, 1, .58f);
            int onShow = shop.DisplayCount(tier);
            string status = (onShow > 0 ? $"展台上 {onShow} 台 · " : "展台上没有 · ") + (ready ? "配件齐" : $"缺 {missing.Count} 样");
            Text ss = Ui.Label(screen.transform, status, 16,
                TextAnchor.MiddleCenter, ready ? Ui.Good : Ui.Warn);
            ss.rectTransform.Anchor(0, 0, 1, 0.26f);
            Ui.Panel(rt, "ShelfRail", ShelfRailColor).rectTransform.Anchor(.04f, .46f, .96f, .466f);
            Image card = Ui.FlatPanel(rt, "Card", Ui.PanelBg2);
            card.rectTransform.Anchor(.04f, .02f, .96f, .445f);
            Money cost = Money.Zero;
            foreach (PartDef p in parts) cost += S.Market.PriceOf(p);
            var sb = new System.Text.StringBuilder();
            foreach (PartDef p in parts)
            {
                int have = shop.Stock.CountAll(p);
                string mark = $"<color=#{ColorUtility.ToHtmlStringRGB(have > 0 ? Ui.Good : Ui.Warn)}>{(have > 0 ? "有" : "缺")}</color>";
                string pn = p.Name.Replace("电源机箱套装", "").Trim();
                sb.Append($"{mark} <b>{PartCatalog.CategoryName(p.Category)}</b> {pn}  ×{have}\n");
            }
            Money retail = shop.BuildPartsRetailValue(tier);
            sb.Append($"<size=12><color=#{ColorUtility.ToHtmlStringRGB(Ui.Dim)}>配件市价参考 {cost}\n盒装分卖合计 {retail}\n整机比配件分卖多收 {shop.BuildPrice(tier) - retail}</color></size>");
            Text ct = Ui.Label(card.transform, sb.ToString(), 14, TextAnchor.MiddleLeft);
            ct.rectTransform.Fill(8);
        }
    }
}
