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
        private static Color GoodsColor(string id)
        {
            switch (id)
            {
                case Gifts.Cig:
                case Gifts.CigHi: return new Color(0.8f, 0.2f, 0.2f);
                case Gifts.Wine:
                case Gifts.Baijiu: return new Color(0.2f, 0.5f, 0.3f);
                case Gifts.Drink:
                case Gifts.Soda: return new Color(0.3f, 0.6f, 0.88f);
                case Gifts.Snack: return new Color(0.9f, 0.62f, 0.25f);
                default: return new Color(0.9f, 0.4f, 0.25f);
            }
        }

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
            int visible=Mathf.Min(3,count);
            for(int i=0;i<visible;i++)
            {
                var icon=Ui.Rect(items,"GoodsIcon_"+id+"_"+i).Anchor(i/3f+.02f,.03f,(i+1)/3f-.02f,.95f);
                DrawShelfGoods(icon,id);
            }
            if(count==0)Ui.Label(items,stock>0?"暂无架上现货":"尚未上架",15,TextAnchor.MiddleCenter,Ui.Dim).rectTransform.Fill();
            ShelfSaleTag(w,rt,"goods_"+id,shop.GoodsPriceOf(id).Yuan,shop.GoodsPriceRange(id),
                value=>shop.GoodsPurchaseIntentAtPrice(id,value),value=>shop.SetGoodsSalePrice(id,value))
                .Anchor(.04f,.14f,.96f,.36f);
            var actions=Ui.Rect(rt,"GoodsActions_"+id).Anchor(.02f,.015f,.98f,.13f);
            var row=Ui.Row(actions,34,3);row.Fill();
            row.name="ShelfGoodsActions_"+id;
            HoldButton(w,row,"放进",Ui.ButtonHot,handy,"件",n=>S.ListGoods(id,(int)n),66,handy>0);
            Ui.Size(Ui.Button(row,"全放进",()=>{S.ListGoods(id,handy);w.RequestRebuild();},14,Ui.ButtonBg,handy>0),76,34);
            HoldButton(w,row,"收回",Ui.ButtonBg,stock,"件",n=>S.UnlistGoods(id,(int)n),66,stock>0);
            if(goods.ShelfDays>0&&stock>0)
            {
                name.text+="\n<size=12><color=#E7BD73>最早到期："+S.GoodsExpiryText(id)+"</color></size>";
                name.fontSize=14;
            }
        }
        private static void DrawShelfGoods(RectTransform root,string id)
        {
            var color=GoodsColor(id);
            if(ClothingProducts.IsClothing(id))
            {
                var outfit=ClothingProducts.Outfit(id);
                if(!ColorUtility.TryParseHtmlString("#"+outfit.ColorHex,out color))color=GoodsColor(id);
                bool shortSleeve=outfit.VisualPreset=="short"||outfit.VisualPreset=="sport";
                Chip(root,color,.24f,.4f,.76f,.94f);
                Chip(root,color,.05f,shortSleeve?.66f:.41f,.25f,.89f);
                Chip(root,color,.75f,shortSleeve?.66f:.41f,.95f,.89f);
                Chip(root,ShelfBack,.39f,.86f,.61f,.99f);
                var trousers=Color.Lerp(color,Color.black,.45f);
                Chip(root,trousers,.27f,.03f,.47f,.39f);Chip(root,trousers,.53f,.03f,.73f,.39f);
                if(outfit.VisualPreset=="winter"||outfit.VisualPreset=="jacket"||outfit.VisualPreset=="formal")
                    Chip(root,Color.Lerp(color,Color.white,.45f),.48f,.4f,.52f,.85f);
            }
            else if(id.StartsWith("flower_"))
            {
                var green=new Color(.25f,.58f,.28f);
                for(int i=0;i<3;i++)
                {
                    float x=.22f+i*.22f;
                    Chip(root,green,x,.23f,x+.05f,.75f);
                    Chip(root,id=="flower_sun"?new Color(.95f,.74f,.16f):id=="flower_lily"?TagPaper:color,x-.12f,.66f,x+.16f,.91f);
                }
                Chip(root,CardboardColor,.22f,.02f,.8f,.32f);
            }
            else if(id==Gifts.Drink||id==Gifts.Soda||id==Gifts.Wine||id==Gifts.Baijiu)
            {
                Chip(root,color,.2f,.03f,.8f,.72f);Chip(root,color,.35f,.7f,.65f,.92f);
                Chip(root,WoodLight,.32f,.91f,.68f,.99f);Chip(root,TagPaper,.23f,.25f,.77f,.53f);
            }
            else if(ModelProducts.IsModel(id))
            {
                Chip(root,TagPaper,.05f,.02f,.95f,.95f);Chip(root,color,.12f,.1f,.88f,.87f);
                Chip(root,TagInk,.34f,.5f,.66f,.77f);Chip(root,TagInk,.25f,.18f,.75f,.51f);
            }
            else
            {
                Chip(root,CardboardColor,.06f,.04f,.94f,.8f);Chip(root,color,.06f,.65f,.94f,.82f);
                Chip(root,TagPaper,.17f,.23f,.83f,.58f);
                Ui.Label(root,Gifts.Get(id)?.Name??"",11,TextAnchor.MiddleCenter,TagInk).rectTransform.Anchor(.17f,.23f,.83f,.58f);
            }
        }
    }
}
