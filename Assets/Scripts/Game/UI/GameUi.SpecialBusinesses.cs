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
        private void OpenIndustryNews() => OpenMarketView(1);
        private void BuildIndustryNews(Window w)
        {
                int day=S.Clock.Today.TotalDays;
                Line(w.Content,"科技发布日历",20,Ui.Accent);
                Para(w.Content,"第31天第二代配件发布，第91天第三代发布。旧代参考价依次下降22%、45%；新品沿用配件品类与兼容规则，已经持有的旧配件继续可用。",17);
                Line(w.Content,"当前主流：第"+(IndustryCycles.CurrentGeneration(day)+1)+"代",18,Ui.Good);
                foreach(string series in ModelProducts.SeriesIds)Para(w.Content,IndustryCycles.ModelNews(series,day)+" · 参考行情 ×"+IndustryCycles.ModelFactor(series,day).ToString("0.##"),17);
                double clothing=IndustryCycles.ClothingFactor(day);
                Para(w.Content,clothing>1?"服装供给事件："+(clothing>1.4?"暴雨运输受阻，参考价上涨50%":"棉花减产，参考价上涨35%")+"。":"服装供给正常，日常参考价稳定；换季会显著改变需求。",17);
        }
        private void BuildIndustryBrief(Transform parent)
        {
            int day=S.Clock.Today.TotalDays;
            if(S.RetailKind=="computer")
            {
                int generation=IndustryCycles.CurrentGeneration(day);
                var brief=Line(parent,"科技行情 · 第"+(generation+1)+"代",18,Ui.Accent);
                brief.Tip(day<30?"第31天新一代配件上市，原有配件参考价下降22%；留意旧货积压。":day<90?"第91天第三代上市；第一代将退出主流，参考价累计下降45%。":"第三代已上市。旧配件仍可销售、装机；低价整机可消化库存。");
            }
            else if(S.RetailKind=="models")
                foreach(string series in ModelProducts.SeriesIds)
                    Line(parent,IndustryCycles.ModelNews(series,day)+" · 行情 ×"+IndustryCycles.ModelFactor(series,day).ToString("0.##"),15,Ui.Accent);
            else if(S.RetailKind=="clothing")
            {
                var seasonLine=Line(parent,"季节经营 · "+CityWeather.Season(S.Clock.Now.TotalMinutes)+"季",18,Ui.Accent);
                seasonLine.Tip("约90%的顾客只选应季服装；换季前准备货品，过季库存可留待下一季或调整售价。");
                double factor=IndustryCycles.ClothingFactor(day);
                if(factor>1)Para(parent,factor>1.4?"暴雨影响运输和供给，服装参考价上涨50%，持续至本年第280天。":"棉花减产影响供给，服装参考价上涨35%，持续至本年第90天。",15,Ui.Warn);
            }
        }
        private void SpecialNumber(Transform parent,string label,long value,long step,long min,long max,Action<long> set,Action refresh=null)
        {
            refresh ??= RebuildDock;
            var row=Ui.Row(parent,34,4);Ui.Size(Ui.Label(row,label+" ¥",16),-1,34,1);
            var input=Ui.TextInput(row,value.ToString(),"金额",text=>{if(long.TryParse(text,out var number)){set(Math.Max(min,Math.Min(max,number)));refresh();}});
            input.contentType=UnityEngine.UI.InputField.ContentType.IntegerNumber;input.characterLimit=10;Ui.Size(input,100,34);
            Ui.Size(Ui.Button(row,"−",()=>{set(Math.Max(min,value-step));refresh();},18,Ui.ButtonBg,value>min),40);
            Ui.Size(Ui.Button(row,"+",()=>{set(Math.Min(max,value+step));refresh();},18,Ui.ButtonBg,value<max),40);
        }

        public void OpenUsedDesk()
        {
            if (!S.InPlayerShop || S.RetailKind!="secondhand") return;
            OpenWindow("收购鉴定台",680,660,w=>
            {
                var items = S.SpecialBusiness(S.CurrentShop.Place.Id).Used.Where(i=>!i.Sold&&!i.Listed)
                    .OrderBy(i=>i.Owned).ToArray();
                if (items.Length == 0) Line(w.Content,"现在没有卖家或待鉴定旧物。",18,Ui.Dim);
                foreach (var item in items)
                    BuildUsedItemAction(w.Content,item,()=>{w.Rebuild();RebuildDock();});
            });
        }
        private string SpecialCounterStructure()
        {
            var state = S.SpecialBusiness(S.CurrentShop.Place.Id);
            if (S.RetailKind == "secondhand")
                return string.Join(",", state.Used.Select(i => $"{i.Id}:{i.Owned}:{i.Inspected}:{i.Listed}:{i.Sold}:{i.Price}:{i.SalePrice}"));

            string orders = string.Join(",", state.Orders.Select(o => $"{o.Id}:{o.Table}:{o.Recipe}:{o.Price}:{o.Stage}:{o.TableCleared}"));
            if (_tab == ShopTab.Orders) return orders;

            long now = S.Clock.Now.TotalMinutes;
            string ready = string.Join(",", state.Orders.Where(o => o.Stage == DiningStage.Cooking && now >= o.ReadyAt).Select(o => o.Id));
            string workers = string.Join(",", state.Workers.Where(w => w.Busy)
                .Select(w => $"{w.ActorId}:{w.OrderId}:{w.Job}:{w.Station}:{w.Carrying}:{w.OperateUntil < 0}"));
            return $"{orders}|{ready}|{workers}|{S.RestaurantCheckoutCustomer?.Id}";
        }

        private void BuildSpecialCounter()
        {
            var shop=S.CurrentShop;var state=S.SpecialBusiness(shop.Place.Id);
            CounterText(() => RetailShopTypes.Name(shop.RetailKind) + " · " + (S.SpecialOpen(shop, S.Clock.Now) ? "营业中" : "未营业"), 21, Ui.Accent);
            ShopHoursRow(shop.Sim);
            int Entrants() => shop.RetailKind == "restaurant"
                ? state.RestaurantArrivalDay == S.Clock.Today.TotalDays ? state.RestaurantArrivalsToday : 0
                : shop.Sim.TodayCustomers;
            CounterText(() => $"今日进店 {Entrants()} 人", 16);
            CounterText(() => $"累计成交 {state.Paid} 单 · 营业收入 ¥{state.Revenue}"
                + (shop.RetailKind == "restaurant" ? $" · 超时流失 {state.Lost}" : ""), 16, Ui.Good);
            if(shop.RetailKind=="restaurant")BuildRestaurantCounter(state);else BuildUsedCounter(state);
        }
        private void BuildRestaurantCounter(SpecialBusinessState state)
        {
            var shop=S.CurrentShop;
            if(S.RestaurantCheckoutCustomer is DiningOrder customer)
            {
                var row=Ui.Row(C,36,4);
                string CheckoutText() => $"收银台 · {S.RestaurantCheckoutCount(shop)} 人排队 · ¥{customer.Price}";
                var queue = Line(row, CheckoutText(), 17, Ui.Accent);
                _counterRefresh.Add(() => queue.text = CheckoutText());
                var checkout=Ui.Size(Ui.Button(row,"结账",()=>_ctrl.CheckoutRestaurant(customer.Id),17,Ui.ButtonHot,
                    _ctrl.Interior?.NearCounter==true),90,36);
                _counterRefresh.Add(()=>checkout.interactable=_ctrl.Interior?.NearCounter==true);
            }
            HelpLink(C,"烹饪说明","在灶台备料投锅，8分钟后熟，38分钟后焦糊。出锅后直接送到餐桌。顾客吃完到收银台排队，可由玩家按 F 或由在岗收银员结账。顾客排队等候60分钟仍未结账就会离开。服务员负责上菜和收桌。");
            CounterText(() => $"厨房大米 {S.KitchenRice(shop):0.##} 千克 · 青菜 {S.KitchenIngredientCount(shop, "veg")}"
                + $" · 鸡蛋 {S.KitchenIngredientCount(shop, "egg")} · 肉 {S.KitchenIngredientCount(shop, "pork")}", 15, Ui.Dim);
            var chill = Ui.Size(Ui.Button(C, "店内食材放入冰箱", () => { S.ChillKitchenIngredients(); RebuildDock(); }, 16, Ui.ButtonHot), -1, 34);
            chill.name = "ChillKitchenIngredients";
            var coldStorageHint = Line(C, "", 14, Ui.Dim);
            var storage = Ui.Row(C, 36, 4);
            var warehouse = Ui.Size(Ui.Button(storage, "全部存入后仓（保留食物）", () => { S.StoreBackpackInWarehouse(); RebuildDock(); }, 15), -1, 36, 1);
            var fridge = Ui.Size(Ui.Button(storage, "打开冰箱", OpenFridgeWindow, 15), 110, 36);
            void RefreshStorage()
            {
                string blocker = S.KitchenColdStorageBlocker;
                chill.interactable = blocker == null;
                coldStorageHint.text = blocker ?? "";
                coldStorageHint.gameObject.SetActive(blocker != null);
                warehouse.interactable = S.CanStoreBackpackInWarehouse;
                fridge.interactable = S.CanOpenFridge;
            }
            RefreshStorage();
            _counterRefresh.Add(RefreshStorage);


            foreach(var order in state.Orders.Where(o=>!o.TableCleared).OrderBy(o=>o.Stage==DiningStage.Cooking&&S.Clock.Now.TotalMinutes>=o.ReadyAt?0:o.Stage==DiningStage.Burnt?1:o.Stage==DiningStage.Ready?2:o.Stage==DiningStage.Waiting?3:4).ToArray())
            {
                int id=order.Id;long now=S.Clock.Now.TotalMinutes;
                var assigned=state.Workers.FirstOrDefault(w=>w.Busy&&w.OrderId==id);
                string Status()
                {
                    long minute = S.Clock.Now.TotalMinutes;
                    if (assigned?.Carrying == true) return "正在送往餐桌";
                    if (order.Stage == DiningStage.Cooking)
                        return minute < order.ReadyAt ? $"烹饪中 · 还有{order.ReadyAt - minute}分钟熟"
                            : $"可出锅 · 距焦糊{Math.Max(0, order.BurnAt - minute)}分钟";
                    return order.Stage switch
                    {
                        DiningStage.Waiting => "等待烹饪",
                        DiningStage.Ready => "已出锅 · 待上菜",
                        DiningStage.Eating => "正在用餐",
                        DiningStage.Checkout => "已离桌 · 待收桌",
                        DiningStage.Completed => "已付款 · 待收桌",
                        DiningStage.Burnt => "焦糊，需重做",
                        _ => "顾客已离开"
                    };
                }
                Line(C,(order.Table+1)+"号桌 · "+GameSession.RecipeNames[order.Recipe]+" · ¥"+order.Price,17,Ui.Accent);
                CounterText(Status, 16, order.Stage == DiningStage.Burnt ? Ui.Warn : Ui.Good);
                bool playerBusy=state.Workers.Any(w=>w.ActorId==GameSession.RestaurantPlayer&&w.Busy);
                if(assigned!=null)
                {
                    string who=assigned.ActorId==GameSession.RestaurantPlayer?"你":S.FindEmployee(assigned.ActorId)?.Person.Name??"员工";
                    string target=FurnitureCatalog.Get(assigned.Station)?.Name??"操作位置";
                    Line(C,who+"："+(assigned.OperateUntil<0?"前往"+target:"正在"+GameSession.RestaurantJobName(assigned.Job)),15,Ui.Good);
                    if(assigned.ActorId==GameSession.RestaurantPlayer)Ui.Button(C,"取消本次操作",()=>{S.CancelPlayerRestaurantWork();RebuildDock();},15);
                }
                void Request(RestaurantJob job){_ctrl.RequestRestaurantWork(id,job);RebuildDock();}
                bool canRequest=assigned==null&&!playerBusy;
                var bar=Ui.Row(C,36,4);
                if(order.Stage==DiningStage.Waiting)Ui.Size(Ui.Button(bar,"备料投锅",()=>Request(RestaurantJob.Cook),17,Ui.ButtonHot,canRequest),-1,36,1);
                if (order.Stage == DiningStage.Cooking)
                {
                    bool CanServe(long minute) => canRequest && minute >= order.ReadyAt && minute < order.BurnAt;
                    var serve = Ui.Size(Ui.Button(bar, "出锅上菜", () => Request(RestaurantJob.Serve), 17, Ui.ButtonHot, CanServe(now)), -1, 36, 1);
                    _counterRefresh.Add(() => serve.interactable = CanServe(S.Clock.Now.TotalMinutes));
                }
                if(order.Stage==DiningStage.Ready)Ui.Size(Ui.Button(bar,"上菜",()=>Request(RestaurantJob.Serve),17,Ui.ButtonHot,canRequest),-1,36,1);
                if(order.NeedsClearing)
                    Ui.Size(Ui.Button(bar,order.Stage==DiningStage.Burnt?"倒掉重做":"收桌",()=>Request(RestaurantJob.Clear),17,Ui.ButtonBg,canRequest),-1,36,1);
            }
            if(state.Orders.Count==0)Para(C,"等待顾客入座；请检查营业时间、厨房设施和餐桌。",17,Ui.Dim);
        }
        private void BuildSpecialPricing()
        {
            var state=S.SpecialBusiness(S.CurrentShop.Place.Id);
            if(S.RetailKind=="secondhand") { BuildUsedStock(C,pricingOnly:true); return; }
            Line(C,"菜单与饮料定价",20,Ui.Accent);
            for(int recipe=0;recipe<GameSession.RecipeNames.Length;recipe++)
            {
                int selected=recipe;bool enabled=(state.RecipeMask&(1<<selected))!=0;
                Ui.Size(Ui.Button(C,(enabled?"✓ 供应 ":"暂停 ")+GameSession.RecipeNames[selected]+" · ¥"+Math.Round(GameSession.RecipePrices[selected]*state.MenuMarkup),()=>{state.RecipeMask^=1<<selected;RebuildDock();},16,enabled?Ui.ButtonHot:Ui.ButtonBg),-1,34);
            }
            BuildMenuMarkupEditor(state);
            BuildRestaurantDrinkPricing();
        }
        private void BuildMenuMarkupEditor(SpecialBusinessState state)
        {
            string key="menu:"+S.CurrentShop.Place.Id;
            double draft=_multiplierDrafts.TryGetValue(key,out var previous)?previous:state.MenuMarkup;var label=Line(C,"",17,Ui.Good);UnityEngine.UI.Button save=null;
            void Preview(){label.text=$"菜单售价 {draft:0.0}× · 购买意愿 {S.PlayerShop.MarkupPurchaseIntent(draft)*100:0}%";if(save!=null)save.interactable=Math.Abs(draft-state.MenuMarkup)>.001;}
            Ui.Slider(C,.8f,(float)PCShopSell.Core.Shop.ShopSettings.MaxMarkup,(float)draft,v=>{draft=Math.Round(v*10)/10;_multiplierDrafts[key]=draft;Preview();}).name="MenuMarkupSlider";
            save=Ui.Size(Ui.Button(C,"确认菜单售价",()=>{state.MenuMarkup=draft;_multiplierDrafts.Remove(key);RebuildDock();},17,Ui.ButtonHot,false),-1,36);
            save.name="SaveMenuMarkup";Preview();
            Line(C,"供应开关即时生效，调整价格后请确认。",14,Ui.Dim);
        }
        private void BuildSpecialOrders()
        {
            var state=S.SpecialBusiness(S.CurrentShop.Place.Id);
            CounterText(() => $"累计成交 {state.Paid} 单 · 营业收入 ¥{state.Revenue}", 19, Ui.Accent);
            if(S.RetailKind=="secondhand")
            {
                Line(C,"收购与售出记录",18,Ui.Accent);
                foreach(var item in state.Used.Where(i=>i.Owned).Reverse())
                    Line(C,GameSession.UsedNames[item.Product]+" #"+item.Id+" · "+(item.Sold?"已售 ¥"+(item.SalePrice>0?item.SalePrice:item.Price):item.Listed?"待售":"处理中")+" · 收购成本 ¥"+item.Cost,16);
                if(!state.Used.Any(i=>i.Owned))Line(C,"暂无收购记录。",16,Ui.Dim);
                return;
            }
            CounterText(() => $"当前订单 {state.Orders.Count} · 超时流失 {state.Lost}", 17);
            foreach(var order in state.Orders.OrderBy(o=>o.Table))
            {
                string status=order.Stage==DiningStage.Waiting?"待备料":order.Stage==DiningStage.Cooking?"烹饪中":order.Stage==DiningStage.Ready?"待上菜":order.Stage==DiningStage.Eating?"用餐中":order.Stage==DiningStage.Checkout?"待收银台结账":order.Stage==DiningStage.Completed?"已付款":order.Stage==DiningStage.Burnt?"已焦糊":"已离开";
                Line(C,$"{order.Table+1}号桌 · {GameSession.RecipeNames[order.Recipe]} · ¥{order.Price} · {status}",17);
            }
            if(state.Orders.Count==0)Line(C,"当前没有桌单。",16,Ui.Dim);
            Ui.Size(Ui.Button(C,"返回柜台处理桌单",()=>SelectTab(ShopTab.Counter),17),-1,38);
        }
        private void BuildUsedCounter(SpecialBusinessState state)
        {
            BuildUsedCheckout();
            HelpLink(C,"二手收购说明","先听卖家说法和报价，再决定收购；买下后才能鉴定。性格影响可信度，不保证真假。鉴定并定价后可在二手转售品货架出售；代管收购仍有亏损风险。");
            bool hasAppraiser = S.ShopStaff(S.CurrentShop, EmployeeRole.Appraiser).Any();
            bool automatic = hasAppraiser && !state.AutoPurchasePaused;
            string delegation = !hasAppraiser ? "收购师代管 · 尚未安排收购师" : automatic ? "收购师代管：开" : "收购师代管：关";
            Ui.Size(Ui.Button(C, delegation, () => { state.AutoPurchasePaused = !state.AutoPurchasePaused; RebuildDock(); },
                17, automatic ? Ui.ButtonHot : Ui.ButtonBg, hasAppraiser), -1, 36);
            SpecialNumber(C,"每日收购预算",state.DailyBudget,100,0,100000,value=>state.DailyBudget=value);
            SpecialNumber(C,"单件最高收购价",state.MaxOffer,10,0,10000,value=>state.MaxOffer=value);
            var risk=Ui.Row(C,34,4);Ui.Size(Ui.Label(risk,"风险倾向："+(state.Risk<.4?"谨慎":state.Risk>.6?"积极":"均衡"),16),-1,34,1);
            foreach(var level in new[]{.2,.5,.8}){double choice=level;Ui.Size(Ui.Button(risk,level<.4?"谨慎":level>.6?"积极":"均衡",()=>{state.Risk=choice;RebuildDock();},15),65);}
            CounterText(() => $"今日收购已支出 ¥{state.SpentToday} · 员工须实际在岗；智力越高判断越可靠，仍可能亏损。", 15, Ui.Dim);
            BuildUsedSalesHistory(C,state);
            Line(C,"收购与待处理物品",19,Ui.Accent);
            Line(C,"收购后鉴定，再定价加入转售库存。",15,Ui.Dim);
            foreach(var item in state.Used.Where(i=>!i.Sold&&!i.Listed).ToArray())
                BuildUsedItemAction(C,item,RebuildDock);
        }

        private void BuildUsedItemAction(Transform parent,UsedItem item,Action refresh)
        {
            int id=item.Id;string key=S.CurrentShop.Place.Id+":"+id;
            Line(parent,GameSession.UsedNames[item.Product]+" #"+id,18,Ui.Accent);
            if(!item.Owned)
            {
                Para(parent,GameSession.SellerNames[Math.Max(0,Math.Min(GameSession.SellerNames.Length-1,item.SellerId))]+" · "+GameSession.SellerPersonalities[item.Personality]+" · "+GameSession.SellerSpeech(item),16);
                Line(parent,"卖家要价 ¥"+item.Asking+" · 状态未鉴定",16,Ui.Warn);
                long bid=_usedBids.TryGetValue(key,out var chosen)?chosen:item.Asking;
                SpecialNumber(parent,"我的报价",bid,1,1,1000000,value=>_usedBids[key]=value,refresh);
                var bar=Ui.Row(parent,36,4);
                Ui.Size(Ui.Button(bar,"出价收购",()=>{S.OfferUsedPrice(id,bid);refresh();},17,Ui.ButtonHot),-1,36,1);
                Ui.Size(Ui.Button(bar,"拒收",()=>{S.DeclineUsedSeller(id);refresh();},17,Ui.Warn),90,36).name="DeclineUsed_"+id;
            }
            else if(!item.Inspected)
            {
                Line(parent,"收购成本 ¥"+item.Cost+" · 尚未鉴定",16,Ui.Dim);
                Ui.Size(Ui.Button(parent,"检查，揭晓状态",()=>{S.InspectUsed(id);refresh();},17,Ui.ButtonHot),-1,36);
            }
            else
            {
                Line(parent,GameSession.ConditionName(item.Condition)+" · 成本 ¥"+item.Cost+" · 参考转售价 ¥"+GameSession.UsedValue(item),16,Ui.Good);
                long price=_usedBids.TryGetValue(key+":sale",out var chosen)?chosen:Math.Max(1,item.Price);
                SpecialNumber(parent,"出售标价",price,1,1,1000000,value=>_usedBids[key+":sale"]=value,refresh);
                var bar=Ui.Row(parent,36,4);
                Ui.Size(Ui.Button(bar,"确认售价并上架",()=>{S.ListUsedItem(id,price);refresh();},17,Ui.ButtonHot),-1,36,1).name="ListUsed_"+id;
                Ui.Size(Ui.Button(bar,"丢弃",()=>ConfirmBusiness("丢弃旧物","将损失这件旧物及收购成本。",()=>{S.DiscardUsed(id);refresh();}),16,Ui.Warn),70,36);
            }
        }
    }
}
