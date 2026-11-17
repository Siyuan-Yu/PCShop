using System.Linq;
using PCShopSell.Core.Home;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Shop;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    /// <summary>货架窗口里杂货那一半：烟酒、饮料、点心、水果，列出货架上摆着的、后仓里的、手边能放进来的。</summary>
    public sealed partial class GameUi
    {
        private void BuildGoodsShelfBody(Window w, ShelfKind kind, ShelfFill fill)
        {
            var shop=S.PlayerShop;
            var products=kind.GiftIds.Select(Gifts.Get).Where(g=>g!=null&&
                (shop.Stock.GoodsCount(g.Id)>0||S.GoodsAvailableToList(g.Id)>0)).ToArray();
            Para(w.Content,"商品摆在下方货架上；价签旁可调价，点确定生效。手边商品可直接放进店里。",15,Ui.Dim);
            var list=Ui.Scroll(w.Content,630);
            int boards=Mathf.Max(2,(products.Length+2)/3);
            var frame=Ui.Panel(list,"GoodsShelf",WoodDark);
            Ui.Size(frame,-1,48+boards*320);Ui.VList(frame.rectTransform,0,10);
            var sign=Ui.Panel(frame.transform,"Sign",WoodLight);Ui.Size(sign,-1,32);
            Ui.Label(sign.transform,"<b>"+kind.Name+"</b>",18,TextAnchor.MiddleCenter,TagInk).rectTransform.Fill();
            for(int board=0;board<boards;board++)
            {
                var row=Ui.Row(frame.transform,300,8);
                for(int column=0;column<3;column++)
                {
                    int index=board*3+column;
                    var slot=Ui.Panel(row,"GoodsSlot_"+index,ShelfBack);Ui.Size(slot,-1,300,1);
                    if(index<products.Length)BuildGoodsShelfSlot(w,slot.rectTransform,products[index],fill);
                }
                Ui.Size(Ui.Panel(frame.transform,"Plank",WoodLight),-1,12);
                Ui.Size(Ui.Rect(frame.transform,"Gap"),-1,8);
            }
            if(products.Length==0)Para(list,"店里和手边还没有这类商品；可从进货渠道采购，送到后仓或从背包上架。",17,Ui.Dim);
        }
        private void BuildGoodsShelfSlot(Window w,RectTransform rt,GiftDef goods,ShelfFill fill)
        {
            string id=goods.Id;var shop=S.PlayerShop;
            int count=fill.GoodsCount(id),stock=shop.Stock.GoodsCount(id),handy=S.GoodsAvailableToList(id);
            var name=Ui.Label(rt,ProductLabel("<b>"+goods.Name+"</b>",goods.WeightKg,
                "架上 "+count+" 件 · 后仓共 "+stock+" 件"+(handy>0?" · 手边 "+handy+" 件":"")),15);
            name.rectTransform.Anchor(.03f,.73f,.97f,.99f);
            Chip(rt,WoodLight,0,.38f,1,.4f);
            var items=Ui.Rect(rt,"GoodsIllustrations").Anchor(.04f,.4f,.96f,.73f);
            int visible=Mathf.Clamp(count,1,3);
            for(int i=0;i<visible;i++)
            {
                var icon=Ui.Rect(items,"GoodsIcon_"+id+"_"+i).Anchor(i/3f+.02f,.03f,(i+1)/3f-.02f,.95f);
                var art = FillItemPortrait(icon, id).Fill();
                if(count==0)art.GetComponent<RawImage>().color = new Color(1,1,1,.55f);
            }
            if(count==0)Ui.Label(items,stock>0?"暂无架上现货":"尚未上架",15,TextAnchor.MiddleCenter,Ui.Dim).rectTransform.Anchor(.34f,0,1,1);
            ShelfSaleTag(w,rt,"goods_"+id,shop.GoodsPriceOf(id).Yuan,shop.GoodsPriceRange(id),
                value=>shop.GoodsPurchaseIntentAtPrice(id,value),value=>shop.SetGoodsSalePrice(id,value))
                .Anchor(.04f,.14f,.96f,.36f);
            var actions=Ui.Rect(rt,"GoodsActions_"+id).Anchor(.02f,.015f,.98f,.13f);
            var row=Ui.Row(actions,34,3);row.Fill();
            row.name="ShelfGoodsActions_"+id;
            HoldButton(w,row,"放进",Ui.ButtonBg,handy,"件",n=>S.ListGoods(id,(int)n),66,handy>0);
            Ui.Size(Ui.Button(row,"全放进",()=>{S.ListGoods(id,handy);w.RequestRebuild();},14,Ui.ButtonBg,handy>0),76,34);
            HoldButton(w,row,"收回",Ui.ButtonBg,stock,"件",n=>S.UnlistGoods(id,(int)n),66,stock>0);
            if(goods.ShelfDays>0&&stock>0)
            {
                name.text+="\n<size=12><color=#80551F>最早到期："+S.GoodsExpiryText(id)+"</color></size>";
                name.fontSize=14;
            }
        }

    }
}
