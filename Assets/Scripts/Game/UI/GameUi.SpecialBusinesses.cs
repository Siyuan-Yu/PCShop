using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using UnityEngine;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        public void OpenSpecialBusinessPanel(){if(S.IsSpecialBusiness(S.CurrentShop)){OpenDock();SelectTab(ShopTab.Counter);}}
        private readonly Dictionary<string,long> _usedBids=new Dictionary<string,long>();
        private void OpenIndustryNews()
        {
            OpenWindow("各行业行情",850,720,w=>{
                int day=S.Clock.Today.TotalDays;
                Line(w.Content,"科技发布日历",20,Ui.Accent);
                Para(w.Content,"第31天第二代配件发布，第91天第三代发布。旧代参考价依次下降22%、45%；新品沿用配件品类与兼容规则，已经持有的旧配件继续可用。",17);
                Line(w.Content,"当前主流：第"+(IndustryCycles.CurrentGeneration(day)+1)+"代",18,Ui.Good);
                foreach(string series in ModelProducts.SeriesIds)Para(w.Content,IndustryCycles.ModelNews(series,day)+" · 参考行情 ×"+IndustryCycles.ModelFactor(series,day).ToString("0.##"),17);
                double clothing=IndustryCycles.ClothingFactor(day);
                Para(w.Content,clothing>1?"服装供给事件："+(clothing>1.4?"暴雨运输受阻，参考价上涨50%":"棉花减产，参考价上涨35%")+"。":"服装供给正常，日常参考价稳定；换季会显著改变需求。",17);
            });
        }
        private void BuildIndustryBrief(Transform parent)
        {
            int day=S.Clock.Today.TotalDays;
            if(S.RetailKind=="computer")
            {
                int generation=IndustryCycles.CurrentGeneration(day);
                Line(parent,"科技行情 · 当前第"+(generation+1)+"代",18,Ui.Accent);
                Para(parent,day<30?"第31天新一代配件上市，原有配件参考价下降22%；留意旧货积压。":day<90?"第91天第三代上市；第一代将退出主流，参考价累计下降45%。":"第三代已上市。旧配件仍可销售、装机；低价整机可消化库存。",15,Ui.Dim);
            }
            else if(S.RetailKind=="models")
                foreach(string series in ModelProducts.SeriesIds)
                    Line(parent,IndustryCycles.ModelNews(series,day)+" · 行情 ×"+IndustryCycles.ModelFactor(series,day).ToString("0.##"),15,Ui.Accent);
            else if(S.RetailKind=="clothing")
            {
                Line(parent,"季节经营 · "+CityWeather.Season(S.Clock.Now.TotalMinutes)+"季",18,Ui.Accent);
                Para(parent,"约90%的顾客只选应季服装；换季前准备货品，过季库存可留待下一季或调整售价。",15,Ui.Dim);
                double factor=IndustryCycles.ClothingFactor(day);
                if(factor>1)Para(parent,factor>1.4?"暴雨影响运输和供给，服装参考价上涨50%，持续至本年第280天。":"棉花减产影响供给，服装参考价上涨35%，持续至本年第90天。",15,Ui.Warn);
            }
        }
        private void SpecialNumber(Transform parent,string label,long value,long step,long min,long max,Action<long> set)
        {
            var row=Ui.Row(parent,34,4);Ui.Size(Ui.Label(row,label+" ¥",16),-1,34,1);
            var input=Ui.TextInput(row,value.ToString(),"金额",text=>{if(long.TryParse(text,out var number)){set(Math.Max(min,Math.Min(max,number)));RebuildDock();}});
            input.contentType=UnityEngine.UI.InputField.ContentType.IntegerNumber;input.characterLimit=10;Ui.Size(input,100,34);
            Ui.Size(Ui.Button(row,"−",()=>{set(Math.Max(min,value-step));RebuildDock();},18,Ui.ButtonBg,value>min),40);
            Ui.Size(Ui.Button(row,"+",()=>{set(Math.Min(max,value+step));RebuildDock();},18,Ui.ButtonBg,value<max),40);
        }
        private void BuildSpecialCounter()
        {
            var shop=S.CurrentShop;var state=S.SpecialBusiness(shop.Place.Id);
            Line(C,RetailShopTypes.Name(shop.RetailKind)+" · "+(S.SpecialOpen(shop,S.Clock.Now)?"营业中":"未营业"),21,Ui.Accent);
            string gap=S.SpecialFitGap(shop);
            if(gap!=null)Para(C,"请先去家具城购买并在装修页摆好："+gap,17,Ui.Warn);
            var manage=Ui.Row(C,38,4);
            Ui.Size(Ui.Button(manage,"经营类型 / 转型",OpenRetailTypeChooser,16),-1,38,1);
            Ui.Size(Ui.Button(manage,shop.OwnsProperty?"结束自营":"退租店铺",()=>ConfirmCloseShop(shop),16,Ui.Warn),110,38);
            ShopHoursRow(shop.Sim);
            Line(C,"累计成交 "+state.Paid+" 单 · 营业收入 ¥"+state.Revenue+(shop.RetailKind=="restaurant"?" · 超时流失 "+state.Lost:""),16,Ui.Good);
            if(shop.RetailKind=="restaurant")BuildRestaurantCounter(state);else BuildUsedCounter(state);
        }
        private void BuildRestaurantCounter(SpecialBusinessState state)
        {
            var shop=S.CurrentShop;
            for(int recipe=0;recipe<GameSession.RecipeNames.Length;recipe++){int selected=recipe;bool enabled=(state.RecipeMask&(1<<selected))!=0;Ui.Size(Ui.Button(C,(enabled?"✓ 供应 ":"暂停 ")+GameSession.RecipeNames[selected]+" · ¥"+Math.Round(GameSession.RecipePrices[selected]*state.MenuMarkup),()=>{state.RecipeMask^=1<<selected;RebuildDock();},15,enabled?Ui.ButtonHot:Ui.ButtonBg),-1,30);}
            Para(C,"加工台备菜 → 投锅 → 熟后及时出锅 → 传菜台上菜 → 顾客吃完付款 → 收桌。锅内8分钟熟，38分钟焦糊；顾客等餐100分钟。暂停时火候也暂停。",15,Ui.Dim);
            var menu=Ui.Row(C,32,4);
            Ui.Size(Ui.Label(menu,"菜单倍率 ×"+state.MenuMarkup.ToString("0.0"),16),-1,32,1);
            Ui.Size(Ui.Button(menu,"−",()=>{state.MenuMarkup=Math.Max(.8,state.MenuMarkup-.1);RebuildDock();},18),40);
            Ui.Size(Ui.Button(menu,"+",()=>{state.MenuMarkup=Math.Min(1.6,state.MenuMarkup+.1);RebuildDock();},18),40);
            Line(C,"厨房大米 "+S.KitchenRice(shop).ToString("0.##")+" 千克 · 青菜 "+S.KitchenIngredientCount(shop,"veg")+" · 鸡蛋 "+S.KitchenIngredientCount(shop,"egg")+" · 肉 "+S.KitchenIngredientCount(shop,"pork"),15,Ui.Dim);
            Ui.Size(Ui.Button(C,"店内食材放入冰箱",()=>{S.ChillKitchenIngredients();RebuildDock();},16,Ui.ButtonHot,S.CanOpenFridge),-1,34);
            var storage=Ui.Row(C,36,4);
            Ui.Size(Ui.Button(storage,"背包全部存入后仓",()=>{S.StoreBackpackInWarehouse();RebuildDock();},15,Ui.ButtonBg,S.HasWarehouseHere),-1,36,1);
            Ui.Size(Ui.Button(storage,"打开冰箱",OpenFridgeWindow,15,Ui.ButtonBg,S.CanOpenFridge),110,36);
            Para(C,"食材放本店冰箱、后仓或经营库存；安排进货员补货。厨师自动投锅出锅，服务员自动上菜收桌，均须实际在岗。",15,Ui.Dim);
            foreach(var order in state.Orders.ToArray())
            {
                int id=order.Id;long now=S.Clock.Now.TotalMinutes;
                string status=order.Stage==DiningStage.Waiting?"等待烹饪":order.Stage==DiningStage.Cooking?(now<order.ReadyAt?"烹饪中 · 还有"+(order.ReadyAt-now)+"分钟熟":"可出锅 · 距焦糊"+(order.BurnAt-now)+"分钟"):
                    order.Stage==DiningStage.Ready?"传菜台待上菜":order.Stage==DiningStage.Eating?"正在用餐":order.Stage==DiningStage.Completed?"已付款，待收桌":order.Stage==DiningStage.Burnt?"焦糊，需重做":"顾客已离开";
                Line(C,(order.Table+1)+"号桌 · "+GameSession.RecipeNames[order.Recipe]+" · ¥"+order.Price,17,Ui.Accent);
                Line(C,status,16,order.Stage==DiningStage.Burnt?Ui.Warn:Ui.Good);
                var bar=Ui.Row(C,36,4);
                if(order.Stage==DiningStage.Waiting)Ui.Size(Ui.Button(bar,"备料投锅",()=>{S.StartRestaurantCooking(id);RebuildDock();},17,Ui.ButtonHot),-1,36,1);
                if(order.Stage==DiningStage.Cooking)Ui.Size(Ui.Button(bar,"出锅到传菜台",()=>{S.PlateRestaurantOrder(id);RebuildDock();},17,Ui.ButtonHot,now>=order.ReadyAt&&now<order.BurnAt),-1,36,1);
                if(order.Stage==DiningStage.Ready)Ui.Size(Ui.Button(bar,"上菜",()=>{S.ServeRestaurantOrder(id);RebuildDock();},17,Ui.ButtonHot),-1,36,1);
                if(order.Stage==DiningStage.Completed||order.Stage==DiningStage.Left||order.Stage==DiningStage.Burnt)
                    Ui.Size(Ui.Button(bar,order.Stage==DiningStage.Burnt?"倒掉重做":"收桌",()=>{S.ClearRestaurantOrder(id);RebuildDock();},17),-1,36,1);
            }
            if(state.Orders.Count==0)Para(C,"等待顾客入座；请检查营业时间、厨房设施和餐桌。",17,Ui.Dim);
        }
        private void BuildUsedCounter(SpecialBusinessState state)
        {
            Para(C,"先听卖家说法，再决定出价。买下后才能鉴定。人格只影响可信度，不保证每次真假；未鉴定的物品不能出售。",15,Ui.Dim);
            Ui.Size(Ui.Button(C,state.AutoPurchase?"收购师代管：开":"收购师代管：关",()=>{state.AutoPurchase=!state.AutoPurchase;RebuildDock();},17,state.AutoPurchase?Ui.ButtonHot:Ui.ButtonBg),-1,36);
            SpecialNumber(C,"每日收购预算",state.DailyBudget,100,0,100000,value=>state.DailyBudget=value);
            SpecialNumber(C,"单件最高收购价",state.MaxOffer,10,0,10000,value=>state.MaxOffer=value);
            var risk=Ui.Row(C,34,4);Ui.Size(Ui.Label(risk,"风险倾向："+(state.Risk<.4?"谨慎":state.Risk>.6?"积极":"均衡"),16),-1,34,1);
            foreach(var level in new[]{.2,.5,.8}){double choice=level;Ui.Size(Ui.Button(risk,level<.4?"谨慎":level>.6?"积极":"均衡",()=>{state.Risk=choice;RebuildDock();},15),65);}
            Line(C,"今日收购已支出 ¥"+state.SpentToday+" · 员工须实际在岗；智力越高判断越可靠，仍可能亏损。",15,Ui.Dim);
            foreach(var sold in state.Used.Where(i=>i.Sold).Reverse().Take(3))Line(C,"已售 "+GameSession.UsedNames[sold.Product]+" · "+GameSession.ConditionName(sold.Condition)+" · 收 "+sold.Cost+" / 售 "+sold.Price+" · 毛利 ¥"+(sold.Price-sold.Cost),15,sold.Price>=sold.Cost?Ui.Good:Ui.Warn);
            foreach(var item in state.Used.Where(i=>!i.Sold).ToArray())
            {
                int id=item.Id;string key=S.CurrentShop.Place.Id+":"+id;
                Line(C,GameSession.UsedNames[item.Product]+" #"+id,18,Ui.Accent);
                if(!item.Owned)
                {
                    Para(C,GameSession.SellerNames[Math.Max(0,Math.Min(GameSession.SellerNames.Length-1,item.SellerId))]+" · "+GameSession.SellerPersonalities[item.Personality]+" · "+GameSession.SellerSpeech(item),16);
                    Line(C,"卖家要价 ¥"+item.Asking+" · 状态未鉴定",16,Ui.Warn);
                    long bid=_usedBids.TryGetValue(key,out var chosen)?chosen:item.Asking;
                    SpecialNumber(C,"我的报价",bid,1,1,1000000,value=>_usedBids[key]=value);
                    var bar=Ui.Row(C,36,4);
                    Ui.Size(Ui.Button(bar,"出价收购",()=>{S.OfferUsedPrice(id,bid);RebuildDock();},17,Ui.ButtonHot),-1,36,1);
                    Ui.Size(Ui.Button(bar,"不收",()=>{S.DeclineUsedSeller(id);RebuildDock();},17),90,36);
                }
                else if(!item.Inspected)
                {
                    Line(C,"收购成本 ¥"+item.Cost+" · 尚未鉴定",16,Ui.Dim);
                    Ui.Size(Ui.Button(C,"检查，揭晓状态",()=>{S.InspectUsed(id);RebuildDock();},17,Ui.ButtonHot),-1,36);
                }
                else
                {
                    Line(C,GameSession.ConditionName(item.Condition)+" · 成本 ¥"+item.Cost+" · 参考转售价 ¥"+GameSession.UsedValue(item),16,Ui.Good);
                    long price=_usedBids.TryGetValue(key+":sale",out var chosen)?chosen:Math.Max(1,item.Price);
                    SpecialNumber(C,"出售标价",price,1,1,1000000,value=>_usedBids[key+":sale"]=value);
                    var bar=Ui.Row(C,36,4);
                    Ui.Size(Ui.Button(bar,item.Listed?"确认改价":"放入销售展示台",()=>{S.ListUsedItem(id,price);RebuildDock();},17,Ui.ButtonHot),-1,36,1);
                    Ui.Size(Ui.Button(bar,"丢弃",()=>ConfirmBusiness("丢弃旧物","将损失这件旧物及收购成本。",()=>{S.DiscardUsed(id);RebuildDock();}),16,Ui.Warn),70,36);
                }
            }
        }
    }
}
