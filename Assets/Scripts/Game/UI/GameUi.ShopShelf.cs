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
    /// <summary>货架窗口和整机展示台窗口：画成真的货架 / 展台的样子，货摆在板子上，价签挂在板边。</summary>
    public sealed partial class GameUi
    {
        private static readonly Color WoodDark = new Color(0.33f, 0.22f, 0.13f);
        private static readonly Color WoodLight = new Color(0.58f, 0.41f, 0.24f);
        private static readonly Color ShelfBack = new Color(0.16f, 0.14f, 0.13f);
        private static readonly Color TagPaper = new Color(0.95f, 0.93f, 0.85f);
        private static readonly Color TagInk = new Color(0.16f, 0.16f, 0.18f);
        private static RectTransform Chip(Transform parent, Color c, float x0, float y0, float x1, float y1)
        {
            Image img = Ui.Panel(parent, "Chip", c);
            return img.rectTransform.Anchor(x0, y0, x1, y1);
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
            OpenWindow("货架", 1000, 1000, w =>
            {
                ShopSimulation shop = S.PlayerShop;
                ShelfKind kind = S.ShelfKindAt(layoutIndex);

                RectTransform head = Ui.Row(w.Content, 44, 8);
                Ui.Size(Ui.Label(head, kind == null ? "这个货架还空着" : "这个货架摆：<b>" + kind.Name + "</b>", 19, TextAnchor.MiddleLeft), -1, -1, 1);
                Ui.Size(Ui.Button(head, kind == null ? "选择摆什么" : "换品类", () => OpenShelfKindChooser(layoutIndex, w), 17, kind == null ? Ui.ButtonHot : Ui.ButtonBg), 160);

                FurnitureDef shelf = FurnitureCatalog.Get(S.ShopLayout[layoutIndex].Id);
                if (kind == null)
                {
                    Line(w.Content, $"最多 {shelf.ShelfItems} 件 · {shelf.ShelfKg:0} 千克", 16, Ui.Dim);
                    Line(w.Content, "选好品类后，从后仓自动上架。", 17, Ui.Dim);
                    Ui.Size(Ui.Button(w.Content, "关闭", w.Close, 20), -1, 46);
                    return;
                }

                ShelfFill fill = shop.ShelfFillAt(layoutIndex,kind) ?? new ShelfFill(kind,shelf.ShelfKg,shelf.ShelfItems);
                Line(w.Content,$"本架 {fill.UsedKg:0.##}/{fill.CapacityKg:0} 千克 · {fill.Total}/{fill.CapacityItems} 件"
                    +(fill.Full?" · 已满":""),17,fill.Full?Ui.Warn:Ui.Accent);
                if(kind.Id=="secondhand")
                {
                    BuildUsedStock(w.Content,w,true,displayed:fill.Resale.Select(o=>o.Id).ToArray());return;
                }

                HelpLink(w.Content, "调价说明", "长按加减号可加速。调价时可预览购买意愿，确认后生效；同型号盒装和散件的售价联动。");
                if (kind.IsParts) BuildPartShelfBody(w, kind, fill);
                else BuildGoodsShelfBody(w, kind, fill);
                Ui.Size(Ui.Button(w.Content, "关闭", w.Close, 20), -1, 46);
            });
        }

        /// <summary>选这个货架摆哪一类货：一个小窗口，点哪类就是哪类。owner 是从货架窗口打开的就一起刷新。</summary>
        public void OpenShelfKindChooser(int layoutIndex, Window owner = null)
        {
            string group=S.ShelfKindAt(layoutIndex)?.BrowseGroup??(S.RetailKind=="computer"?"电脑配件":S.RetailKind=="clothing"?"服装":S.RetailKind=="models"?"模型手办":S.RetailKind==PetShopCatalog.ShopKind?"宠物用品":"零售商品");
            OpenWindow("这个货架摆什么",760,740,w=>
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
                var choices=Ui.Scroll(w.Content);
                var area=Ui.Size((RectTransform)choices.parent.parent,-1,160).GetComponent<LayoutElement>();
                area.preferredHeight=430;area.flexibleHeight=1;
                void Pick(string id)
                {
                    if(!S.SetShelfKind(layoutIndex,id))return;
                    w.Close();owner?.Rebuild();RebuildDock();
                }
                foreach(var section in ShelfKinds.All.Where(k=>group=="全部" || k.BrowseGroup==group).GroupBy(k=>k.BrowseGroup))
                {
                    Line(choices,section.Key,16,Ui.Accent);
                    RectTransform row=null;int n=0;
                    foreach(var kind in section)
                    {
                        var option=kind;
                        if(n++%2==0)row=Ui.Row(choices,56,6);
                        int count=kind.Id=="secondhand"?S.SpecialBusiness(S.CurrentShop.Place.Id).Used.Count(i=>i.Owned&&!i.Sold):kind.IsParts?PartCatalog.InCategory(kind.Category).Sum(p=>shop.Stock.CountAll(p)):kind.GiftIds.Sum(id=>shop.Stock.GoodsCount(id));
                        int shelves=S.ShelvesOfKind(kind.Id);
                        string info=$"在库 {count} 件 · {(shelves==0?"尚未陈列":shelves+" 个货架")}";
                        var button=Ui.Size(Ui.Button(row,kind.Name+(current?.Id==kind.Id?" ✓":"")+"\n<size=13>"+info+"</size>",()=>Pick(option.Id),17,current?.Id==kind.Id?Ui.ButtonHot:Ui.ButtonBg),-1,-1,1);
                        button.name="ShelfKind_"+kind.Id;
                    }
                    if(n%2==1)Ui.Size(Ui.Rect(row,"Pad"),-1,-1,1);
                }
                var actions=Ui.Row(w.Content,46,8);
                Ui.Size(Ui.Button(actions,"清空这个货架",()=>Pick(null),17,Ui.ButtonBg,current!=null),-1,46,1);
                Ui.Size(Ui.Button(actions,"取消",w.Close,17),160,46);
            },scrollContent:false);
        }

        private void BuildPartShelfBody(Window w, ShelfKind kind, ShelfFill fill)
        {
            ShopSimulation shop = S.PlayerShop;
            var parts = PartCatalog.InCategory(kind.Category).OrderBy(p => p.Tier).ToArray();
            RectTransform list = Ui.Scroll(w.Content, 600);
            for (int board = 0; board < 2; board++)
            {
                bool boxed = board == 0;
                var sign = Ui.Size(Ui.Panel(list, "ShelfSection", WoodDark), -1, 34);
                Ui.Label(sign.transform, boxed ? "整盒" : "散件", 18, TextAnchor.MiddleLeft, Ui.TextColor)
                    .rectTransform.Fill(10);
                var stocked = parts.Where(part => fill.PartCount(part, boxed) > 0).ToArray();
                if (stocked.Length == 0)
                    Line(list, "暂无陈列", 16, Ui.Dim);
                foreach (PartDef part in stocked)
                    BuildShelfSlot(w, list, part, boxed, shop, fill.PartCount(part, boxed));
            }
        }

        private void BuildShelfSlot(Window w, Transform parent, PartDef part, bool boxed, ShopSimulation shop, int count)
        {
            RectTransform row = Ui.Row(parent, 86, 12);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 8, 8);
            row.gameObject.AddComponent<Image>().color = ShelfBack;

            layout.childForceExpandHeight = false;
            ItemPortrait(row, part.Id, 64).name = "PartArtwork";

            string details = $"{PartCatalog.TierName(part.Tier)} · {(boxed ? "盒装" : "散件")} · 架上 {count} 件 · 每件 {PartUnitKg(part, boxed):0.##} 千克";
            Ui.Size(Ui.Label(row, $"<b>{part.Name}</b>\n<size=15>{details}</size>", 20, TextAnchor.MiddleLeft), 0, -1, 1);
            var tag = ShelfSaleTag(w, row, part.Id + (boxed ? "_boxed" : "_loose"), shop.PriceOf(part, boxed).Yuan,
                shop.PartPriceRange(part, boxed), value => shop.PartPurchaseIntentAtPrice(part, boxed, value),
                value => shop.SetPartSalePrice(part, boxed, value));
            Ui.Size(tag, 306);
        }
        // ---------- 整机展示台 ----------

        /// <summary>展示台上的整机：三台开着的整机并排，屏幕上是档次和成交价，台面下的牌子写配置、配件齐不齐。</summary>
        public void OpenDisplayWindow()
        {
            int selected=S.DisplayIndexes().Count>0?S.DisplayIndexes()[0]:-1;
            OpenWindow("展示台", 980, 940, w =>
            {
                var indexes=S.DisplayIndexes();
                if(indexes.Count==0){Para(w.Content,"先购买并摆放一张展示台，再选择展示品类。",19);return;}
                if(!indexes.Contains(selected))selected=indexes[0];
                for(int i=0;i<indexes.Count;i+=3)
                {
                    var selector=Ui.Row(w.Content,40,6);
                    foreach(int layoutIndex in indexes.Skip(i).Take(3))
                    {
                        int index=layoutIndex;
                        string name=S.IsComputerDisplay(index)?"电脑整机":ShelfKinds.NameOf(S.ShopLayout[index].Item);
                        Ui.Size(Ui.Button(selector,"展示台 "+(indexes.ToList().IndexOf(index)+1)+" · "+name,()=>{selected=index;w.Rebuild();},16,
                            selected==index?Ui.ButtonHot:Ui.ButtonBg),-1,40,1);
                    }
                }
                Ui.Size(Ui.Button(w.Content,"更换这张展示台的品类",()=>OpenDisplayKindPicker(selected,w),18,Ui.ButtonBg),-1,42);
                if(!S.IsComputerDisplay(selected)){BuildGoodsDisplay(w,selected);return;}
                ShopSimulation shop = S.PlayerShop;
                int shown = shop.DisplayPcs.Count;
                Para(w.Content, "用库存配件装好整机摆上展示台（店正中间）：想买整机的顾客看到就直接抱去收银台结账，不用等你装。" +
                                $"展台上的机器越多越显眼，想买整机的顾客也越多。展台没有合适的机器且订单未满时，顾客会下装机单（{shop.Settings.BuildOrderDueDays} 天内交付）。", 15, Ui.Dim);
                Para(w.Content, "整机售价 = 同套配件盒装分卖总价 × 1.20 + 装机手工费。配件调价会同步影响新整机报价。", 15, Ui.Accent);
                Line(w.Content, $"展台上 <b>{shown}/{shop.DisplaySlots}</b> 台　·　想买整机的顾客占 <b>{shop.BuildChance:P0}</b>（展台空着时 {shop.Settings.BuildOrderChance:P0}）", 17);

                Image table = Ui.Panel(w.Content, "Table", Ui.FlatBand);
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
            OpenWindow("选择展示品类",640,720,w=>
            {
                void Option(string id,string name)
                {
                    Ui.Size(Ui.Button(w.Content,name,()=>{if(S.SetDisplayKind(layoutIndex,id)){w.Close();parent.Rebuild();RebuildDock();}},18),-1,42);
                }
                Option("computer","电脑整机（装好后展示）");
                foreach(var kind in ShelfKinds.All)
                    Option(kind.Id,kind.Name);
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
            var list=Ui.Scroll(w.Content,420);
            if(fill.Total==0)Para(list,"后仓暂时没有这一品类的商品，可先批发进货，再到“库存”中上架。",18,Ui.Dim);
            foreach(var item in fill.Goods)
            {
                var product=Gifts.Get(item.Key);if(product==null)continue;
                var row=Ui.Row(list,74,10);
                ItemPortrait(row, product.Id, 54);
                Ui.Size(ProductText(row,ProductLabel(product.Name,product.WeightKg,"陈列 "+item.Value+" 件"),18),-1,-1,1);
                Ui.Size(Ui.Label(row,S.PlayerShop.GoodsPriceOf(item.Key).ToString(),20,TextAnchor.MiddleLeft,Ui.Good),100);
            }
            foreach(var line in fill.Parts)
            {
                var row=Ui.Row(list,74,10);
                ItemPortrait(row, line.Part.Id, 54);
                Ui.Size(ProductText(row,ProductLabel(line.Part.Name,PartUnitKg(line.Part,line.Boxed),"陈列 "+line.Count+" 件"),18),-1,-1,1);
                Ui.Size(Ui.Label(row,S.PlayerShop.PriceOf(line.Part,line.Boxed).ToString(),20,TextAnchor.MiddleLeft,Ui.Good),100);
            }
            Ui.Size(Ui.Button(w.Content,"从背包或后仓上架商品",OpenRetailListing,18),-1,42);
            Ui.Size(Ui.Button(w.Content,"关闭",w.Close,18),-1,42);
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

            Image col = Ui.Panel(row, "Stand", Ui.PanelBg2);
            Ui.Size(col, 294, 450);
            RectTransform rt = col.rectTransform;

            // 显示器
            Image bezel = Ui.Panel(rt, "Bezel", new Color(0.05f, 0.05f, 0.07f));
            bezel.rectTransform.Anchor(0.06f, 0.56f, 0.94f, 0.98f);
            Image screen = Ui.Panel(bezel.transform, "Screen", screens[tier]);
            screen.rectTransform.Fill(7);
            Text sn = Ui.Label(screen.transform, $"<b>{PartCatalog.TierName(tier)}整机</b>", 26, TextAnchor.MiddleCenter);
            FillItemPortrait(screen.transform, "pcbuild:" + tier).Anchor(.04f, .25f, .45f, .92f);
            sn.rectTransform.Anchor(.46f, .55f, 1, 1);
            Text sp = Ui.Label(screen.transform, shop.BuildPrice(tier).ToString(), 30, TextAnchor.MiddleCenter, new Color(0.7f, 1f, 0.75f));
            sp.rectTransform.Anchor(.46f, .25f, 1, .58f);
            int onShow = shop.DisplayCount(tier);
            string status = (onShow > 0 ? $"展台上 {onShow} 台 · " : "展台上没有 · ") + (ready ? "配件齐" : $"缺 {missing.Count} 样");
            Text ss = Ui.Label(screen.transform, status, 16,
                TextAnchor.MiddleCenter, onShow > 0 || ready ? new Color(0.85f, 1f, 0.88f) : new Color(1f, 0.78f, 0.65f));
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
            Money retail = shop.BuildPartsRetailValue(tier);
            sb.Append($"<size=12><color=#6B6F78>配件市价参考 {cost}\n盒装分卖合计 {retail}\n整机比配件分卖多收 {shop.BuildPrice(tier) - retail}</color></size>");
            Text ct = Ui.Label(card.transform, sb.ToString(), 14, TextAnchor.MiddleLeft, TagInk);
            ct.rectTransform.Fill(8);
        }
    }
}
