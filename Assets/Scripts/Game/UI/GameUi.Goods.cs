using System.Linq;
using PCShopSell.Core.Home;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Shop;
using UnityEngine;

namespace PCShopSell.Game.UI
{
    /// <summary>货架窗口里杂货那一半：烟酒、饮料、点心、水果，列出货架上摆着的、后仓里的、手边能放进来的。</summary>
    public sealed partial class GameUi
    {
        private void BuildGoodsShelfBody(Window w, ShelfKind kind, ShelfFill fill, int page, System.Action<int> changePage)
        {
            var shop=S.PlayerShop;
            var products=kind.GiftIds.Select(Gifts.Get).Where(g=>g!=null&&
                (shop.Stock.GoodsCount(g.Id)>0||S.GoodsAvailableToList(g.Id)>0)).ToArray();
            if (products.Length == 0)
            {
                var empty = Ui.Size(Ui.FlatPanel(w.Content, "GoodsShelf", Ui.FlatBand), -1, 94);
                Ui.Label(empty.transform, "暂无这类商品。进货后，后仓商品会自动上架。", 17,
                    TextAnchor.MiddleCenter, Ui.Dim).rectTransform.Fill(12);
                return;
            }

            int columns = products.Length <= 6 ? 3 : 4;
            int pageSize = columns * 3;
            const int slotHeight = 198, railHeight = 10, gap = 5, padding = 10;
            int pages = (products.Length + pageSize - 1) / pageSize;
            page = Mathf.Clamp(page, 0, pages - 1);
            int boards = (Mathf.Min(pageSize, products.Length - page * pageSize) + columns - 1) / columns;
            int today = S.Clock.Today.TotalDays;
            bool hasYesterday = S.PriceDays.Any(day => day.Day == today - 1);
            var frame = Ui.FlatPanel(w.Content, "GoodsShelf", Ui.WoodBoard);
            Ui.Size(frame, -1, padding * 2 + boards * (slotHeight + railHeight) + (boards * 2 - 1) * gap);
            Ui.VList(frame.rectTransform, gap, padding);
            for(int board=0;board<boards;board++)
            {
                var row = Ui.Row(frame.transform, slotHeight, 8);
                ShelfBacking(row);
                for(int column = 0; column < columns; column++)
                {
                    int index = page * pageSize + board * columns + column;
                    if (index >= products.Length)
                    {
                        Ui.Size(Ui.Rect(row, "EmptyShelfSlot"), 0, slotHeight, 1);
                        continue;
                    }
                    var slot = Ui.Size(Ui.FlatPanel(row, "GoodsSlot_" + index, Ui.PanelBg), 0, slotHeight, 1);
                    var product = products[index];
                    double? change = hasYesterday
                        ? (IndustryCycles.GoodsFactor(product, today, S.Market.CommoditySeed)
                            / IndustryCycles.GoodsFactor(product, today - 1, S.Market.CommoditySeed) - 1) * 100
                        : (double?)null;
                    BuildGoodsShelfSlot(w, slot.rectTransform, product, fill, change);
                }
                ShelfBoard(frame.transform, railHeight);
            }
            BuildCompactPageControls(w.Content, page, pages, changePage);
        }
        private void BuildGoodsShelfSlot(Window w,RectTransform rt,GiftDef goods,ShelfFill fill,double? change)
        {
            string id=goods.Id;var shop=S.PlayerShop;
            int count=fill.GoodsCount(id),stock=shop.Stock.GoodsCount(id),handy=S.GoodsAvailableToList(id);
            var icon = Ui.FlatPanel(rt, "GoodsIcon_" + id + "_0", Ui.FlatWell);
            icon.rectTransform.Anchor(0, 1, 0, 1, 10, -83, -82, 11);
            FillItemPortrait(icon.transform, id).Fill(4);

            string shelfCount = count > 0 ? $"架上 {count} 件" : "尚未上架";
            string detail = $"{shelfCount} · 后仓共 {stock} 件" + (handy > 0 ? $" · 手边 {handy} 件" : "");
            string text = ProductLabel("<b>" + goods.Name + "</b>", goods.WeightKg, detail,
                weightSuffix: ShelfMarketMovement(change));
            if (goods.ShelfDays > 0 && stock > 0)
                text += $"\n<size=12><color=#{ColorUtility.ToHtmlStringRGB(Ui.Accent)}>最早到期：{S.GoodsExpiryText(id)}</color></size>";
            var name = Ui.Label(rt, text, 15, TextAnchor.UpperLeft);
            name.rectTransform.Anchor(0, .51f, 1, 1, 94, 0, 10, 12);
            ShelfSaleTag(w,rt,"goods_"+id,shop.GoodsPriceOf(id).Yuan,shop.GoodsPriceRange(id),
                value=>shop.GoodsPurchaseEstimateAtPrice(id,value),value=>shop.SetGoodsSalePrice(id,value))
                .Anchor(.035f,.206f,.965f,.48f);
            var actions=Ui.Rect(rt,"GoodsActions_"+id).Anchor(.02f,.015f,.98f,.155f);
            var row=Ui.Row(actions,30,3);row.Fill();
            row.name="ShelfGoodsActions_"+id;
            HoldButton(w,row,"放进",Ui.ButtonBg,handy,"件",n=>S.ListGoods(id,(int)n),66,handy>0);
            Ui.Size(Ui.Button(row,"全放进",()=>{S.ListGoods(id,handy);w.RequestRebuild();},14,Ui.ButtonBg,handy>0),76);
            HoldButton(w,row,"收回",Ui.ButtonBg,stock,"件",n=>S.UnlistGoods(id,(int)n),66,stock>0);
        }

    }
}
