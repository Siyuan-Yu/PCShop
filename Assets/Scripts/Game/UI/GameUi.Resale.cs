using System;
using System.Linq;
using PCShopSell.Core.Session;
using PCShopSell.Core.Inventory;
using UnityEngine;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private void BuildUsedCheckout()
        {
            Line(C,$"今日进店 {S.PlayerShop.TodayCustomers} 人 · 等待结账 {S.PlayerShop.Queue.Count} 人 · 今日营业额 {S.PlayerShop.TodaySales}",16,Ui.Good);
            foreach(var customer in S.PlayerShop.Queue)
            {
                int id=customer.Id;var row=Ui.Row(C,58,5);
                Ui.Size(Ui.Label(row,customer.ItemName+"\n应收 "+customer.Spend,16),-1,58,1);
                var checkout=Ui.Button(row,"结账",()=>_ctrl.Checkout(id),17,Ui.ButtonHot,_ctrl.Interior!=null&&_ctrl.Interior.NearCounter);
                Ui.Size(checkout,90,40).name="UsedCheckout_"+id;
                _counterRefresh.Add(()=>checkout.interactable=_ctrl.Interior!=null&&_ctrl.Interior.NearCounter);
            }
            if(S.PlayerShop.Queue.Count>0)Para(C,"走到收银台旁按 F 或点击结账；也可安排收银员实际在岗收款。",14,Ui.Dim);
        }
        private void BuildUsedSalesHistory(Transform parent,SpecialBusinessState state)
        {
            foreach(var sold in state.Used.Where(i=>i.Sold).Reverse().Take(5))
            {
                long price=sold.SalePrice>0?sold.SalePrice:sold.Price;
                Line(parent,"已售出 · "+GameSession.UsedNames[sold.Product]+" #"+sold.Id+" · 收入 ¥"+price+" · 成本 ¥"+sold.Cost+" · 毛利 ¥"+(price-sold.Cost),15,price>=sold.Cost?Ui.Good:Ui.Warn).name="UsedSold_"+sold.Id;
            }
        }
        private void BuildUsedStock(Transform parent,Window owner=null,bool shelfOnly=false)
        {
            var state=S.SpecialBusiness(S.CurrentShop.Place.Id);
            var onShelf=S.PlayerShop.ShelfFillOf(ShelfKinds.Get("secondhand")).Resale.Select(i=>i.Id).ToArray();
            Line(parent,"二手转售库存",19,Ui.Accent);
            var items=state.Used.Where(i=>i.Owned&&!i.Sold&&(!shelfOnly||i.Listed)).ToArray();
            Transform container=parent;
            if(shelfOnly)
            {
                var frame=Ui.Panel(parent,"UsedShelf",WoodDark).rectTransform;Ui.VList(frame,8,10);container=frame;
            }
            foreach(var item in items)
            {
                int id=item.Id;bool reserved=S.UsedReserved(S.CurrentShop,id);
                string status=reserved?"顾客已拿取 · 等待收银":onShelf.Contains(id)?"已上架 · 等待挑选":item.Listed?"等待货架空位":item.Inspected?"已鉴定 · 待定价上架":"待鉴定";
                var card=Ui.Panel(container,"UsedStock_"+id,shelfOnly?ShelfBack:Ui.PanelBg2).rectTransform;Ui.VList(card,5,8);
                if(shelfOnly)
                {
                    var icon=Ui.Rect(card,"UsedIllustration_"+id);Ui.Size(icon,-1,85);
                    DrawUsedIcon(icon,item.Product);
                }
                Line(card,GameSession.UsedNames[item.Product]+" #"+id+" · "+status,17,Ui.Accent);
                Line(card,(item.Inspected?GameSession.ConditionName(item.Condition):"状态待鉴定")+" · "+GameSession.UsedWeight(item).ToString("0.##")+" 千克 · 成本 ¥"+item.Cost+" · 标价 ¥"+item.Price,15);
                var row=Ui.Row(card,34,5);
                Ui.Size(Ui.Button(row,item.Listed?"调整售价":"定价并上架",()=>OpenUsedPrice(id,owner),16,Ui.ButtonHot,item.Inspected&&!reserved),-1,34,1);
                if(item.Listed)Ui.Size(Ui.Button(row,"下架",()=>{S.UnlistUsedItem(id);owner?.Rebuild();RebuildDock();},16,Ui.ButtonBg,!reserved),85,34);
                if(shelfOnly)Ui.Size(Ui.Panel(container,"Plank",WoodLight),-1,10);
            }
            if(items.Length==0)Line(parent,"暂无二手转售库存。",16,Ui.Dim);
            BuildUsedSalesHistory(parent,state);
        }
        private static void DrawUsedIcon(RectTransform root,int product)
        {
            Color dark=new Color(.22f,.25f,.24f),gold=new Color(.8f,.66f,.38f);
            if(product==0){Chip(root,gold,.32f,.6f,.68f,.94f);Chip(root,dark,.48f,.18f,.52f,.65f);Chip(root,dark,.3f,.1f,.7f,.19f);}
            else if(product==1){Chip(root,dark,.34f,.08f,.66f,.93f);Chip(root,Color.gray,.4f,.45f,.6f,.82f);Chip(root,Color.black,.43f,.17f,.57f,.4f);}
            else if(product==2){Chip(root,new Color(.45f,.3f,.2f),.45f,.02f,.55f,.98f);Chip(root,gold,.35f,.3f,.65f,.72f);Chip(root,Color.white,.39f,.35f,.61f,.67f);Chip(root,dark,.49f,.47f,.51f,.62f);Chip(root,dark,.49f,.47f,.57f,.5f);}
            else {Chip(root,gold,.4f,.3f,.6f,.85f);Chip(root,new Color(.55f,.38f,.23f),.3f,.1f,.7f,.3f);}
        }
        private void OpenUsedPrice(int id,Window owner)
        {
            var item=S.SpecialBusiness(S.CurrentShop.Place.Id).Used.FirstOrDefault(i=>i.Id==id&&!i.Sold);if(item==null)return;
            long price=Math.Max(1,item.Price);
            OpenWindow("二手商品售价",600,330,w=>
            {
                Line(w.Content,GameSession.UsedNames[item.Product]+" · "+GameSession.ConditionName(item.Condition),20,Ui.Accent);
                Line(w.Content,"收购成本 ¥"+item.Cost+" · 建议售价 ¥"+GameSession.UsedValue(item),17);
                var row=Ui.Row(w.Content,40,6);
                Ui.Size(Ui.Button(row,"−",()=>{price=Math.Max(1,price-1);w.Rebuild();},18),55);
                var input=Ui.TextInput(row,price.ToString(),"售价",text=>{if(long.TryParse(text,out long value))price=Math.Max(1,Math.Min(1000000,value));});
                input.contentType=UnityEngine.UI.InputField.ContentType.IntegerNumber;Ui.Size(input,-1,40,1);
                Ui.Size(Ui.Button(row,"+",()=>{price=Math.Min(1000000,price+1);w.Rebuild();},18),55);
                Ui.Size(Ui.Button(w.Content,"确认售价并上架",()=>{if(S.ListUsedItem(id,price)){w.Close();owner?.Rebuild();RebuildDock();}},18,Ui.ButtonHot),-1,42);
            });
        }
    }
}
