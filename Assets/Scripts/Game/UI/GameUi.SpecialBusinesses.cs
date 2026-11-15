using System;
using System.Linq;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using UnityEngine;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        public void OpenSpecialBusinessPanel(){if(S.IsSpecialBusiness(S.CurrentShop)){OpenDock();SelectTab(ShopTab.Counter);}}
        private void OpenIndustryNews() => OpenMarketView(1);
        private void BuildIndustryNews(Window w)
        {
                int day=S.Clock.Today.TotalDays;
                foreach(string series in ModelProducts.SeriesIds)Para(w.Content,IndustryCycles.ModelNews(series,day)+" · 参考行情 ×"+IndustryCycles.ModelFactor(series,day).ToString("0.##"),17);
                double clothing=IndustryCycles.ClothingFactor(day);
                Para(w.Content,clothing>1?"服装供给事件："+(clothing>1.4?"暴雨运输受阻，参考价上涨50%":"棉花减产，参考价上涨35%")+"。":"服装供给正常，日常参考价稳定；换季会显著改变需求。",17);
        }
        private void BuildIndustryBrief(Transform parent)
        {
            int day=S.Clock.Today.TotalDays;
            if(S.RetailKind=="models")
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
            return $"{orders}|{ready}|{workers}";
        }

        private void BuildSpecialCounter()
        {
            var shop=S.CurrentShop;var state=S.SpecialBusiness(shop.Place.Id);
            ShopHoursRow(shop.Sim);
            if (shop.RetailKind == "restaurant") BuildRestaurantCounter(state);
            else BuildUsedCounter(state);
        }
        private void OpenRestaurantHelp() =>
            OpenWorkspace("help", "餐馆经营", "餐馆经营", 720, 580, w =>
                Para(w.Content, "食材存进冰箱，备料时自动取用。灶台下锅后，8分钟熟，38分钟焦糊。出锅后直接上菜。服务员可上菜、收桌并收款，你也可以到餐桌旁按 F 或在柜台面板收桌。店里需要摆放收银台才能收款。", 18));

        private void BuildRestaurantCounter(SpecialBusinessState state)
        {
            var shop=S.CurrentShop;
            if (S.CanOpenFridge)
            {
                var storage = Ui.Row(C, 34, 6);
                var fill = Ui.Size(Ui.Label(storage, "", 15, color: Ui.Dim), -1, 34, 1);
                void RefreshFridge() => fill.text = $"冰箱 {shop.Fridge.WeightKg:0.##} / {S.CurrentFridgeLimitKg:0.##} 千克";
                RefreshFridge();
                _counterRefresh.Add(RefreshFridge);
                Ui.Size(Ui.Button(storage, "打开冰箱", OpenFridgeWindow, 15), 96, 34);
            }

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
                        DiningStage.Checkout => "用餐完毕 · 待收桌收款",
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
                if (order.Stage is not (DiningStage.Waiting or DiningStage.Cooking or DiningStage.Ready) && !order.NeedsClearing) continue;
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
                    Ui.Size(Ui.Button(bar,order.Stage==DiningStage.Burnt?"倒掉重做":order.Stage==DiningStage.Checkout?"收桌并收款":"收桌",()=>Request(RestaurantJob.Clear),17,Ui.ButtonBg,canRequest),-1,36,1);
            }
            if(!state.Orders.Any(o => !o.TableCleared)) Line(C, "暂无在桌订单", 16, Ui.Dim);
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
                foreach (var item in state.Used.Where(i => i.Owned).Reverse())
                {
                    if (item.Sold) BuildUsedSale(C, item);
                    else Line(C, $"{UsedItemTitle(item)} · {(item.Listed ? "待售" : "处理中")} · 收购成本 ¥{item.Cost}", 16);
                }
                if(!state.Used.Any(i=>i.Owned))Line(C,"暂无收购记录。",16,Ui.Dim);
                return;
            }
            CounterText(() => $"当前订单 {state.Orders.Count} · 累计流失 {state.Lost}", 17);
            foreach(var order in state.Orders.OrderBy(o=>o.Table))
            {
                string status=order.Stage==DiningStage.Waiting?"待备料":order.Stage==DiningStage.Cooking?"烹饪中":order.Stage==DiningStage.Ready?"待上菜":order.Stage==DiningStage.Eating?"用餐中":order.Stage==DiningStage.Checkout?"待收桌收款":order.Stage==DiningStage.Completed?"已付款":order.Stage==DiningStage.Burnt?"已焦糊":"已离开";
                Line(C,$"{order.Table+1}号桌 · {GameSession.RecipeNames[order.Recipe]} · ¥{order.Price} · {status}",17);
            }
            if(state.Orders.Count==0)Line(C,"当前没有桌单。",16,Ui.Dim);
            Ui.Size(Ui.Button(C,"返回柜台处理桌单",()=>SelectTab(ShopTab.Counter),17),-1,38);
        }
    }
}
