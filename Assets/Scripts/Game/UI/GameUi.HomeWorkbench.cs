using System;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class WorkbenchHistoryGraphic : MaskableGraphic
    {
        public double[] Values=Array.Empty<double>();
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();if(Values==null||Values.Length==0)return;
            var rect=rectTransform.rect;double low=Values.Min(),high=Values.Max(),span=Math.Max(.01,high-low);
            Vector2 Point(int i)=>new Vector2(rect.xMin+5+(rect.width-10)*(Values.Length==1?.5f:i/(float)(Values.Length-1)),
                rect.yMin+5+(rect.height-10)*(high==low?.5f:(float)((Values[i]-low)/span)));
            void Quad(Vector2 a,Vector2 b,Vector2 c,Vector2 d)
            {int offset=mesh.currentVertCount;foreach(var p in new[]{a,b,c,d}){var v=UIVertex.simpleVert;v.position=p;v.color=color;mesh.AddVert(v);}mesh.AddTriangle(offset,offset+1,offset+2);mesh.AddTriangle(offset,offset+2,offset+3);}
            if(Values.Length==1){var p=Point(0);Quad(p+new Vector2(-2,-2),p+new Vector2(-2,2),p+new Vector2(2,2),p+new Vector2(2,-2));return;}
            for(int i=1;i<Values.Length;i++){var a=Point(i-1);var b=Point(i);var normal=Vector2.Perpendicular(b-a).normalized*1.3f;Quad(a-normal,a+normal,b+normal,b-normal);}
        }
    }
    public sealed partial class GameUi
    {
        private Window _homeWorkbench;
        private Button _workbenchLauncher;
        private int _workbenchTab,_workbenchPeriod,_workbenchBusinessTab;
        private string _workbenchShop;
        private static readonly Color WbPaper=new Color32(240,237,223,255),WbCard=new Color32(250,247,238,255),WbInk=new Color32(41,61,54,255),WbDim=new Color32(101,115,105,255);
        private static Text WbText(Transform parent,string text,int size=18,Color? color=null)
        {var t=Ui.Label(parent,text,size,TextAnchor.MiddleLeft,color??WbInk);Ui.Size(t,-1,size*1.6f);return t;}
        private static Button WbButton(Transform parent,string label,Action action,string name=null,bool selected=false,bool enabled=true)
        {
            var b=Ui.Size(Ui.Button(parent,label,action,17,selected?Ui.ButtonHot:new Color32(224,232,220,255),enabled),-1,40,1);
            b.GetComponentInChildren<Text>().color=selected?Ui.TextColor:WbInk;if(name!=null)b.name=name;return b;
        }
        private static RectTransform WbScroll(Transform parent,string name)
        {
            var list=Ui.Scroll(parent);var area=(RectTransform)list.parent.parent;area.name=name;
            var layout=Ui.Size(area).GetComponent<LayoutElement>();layout.minHeight=80;layout.flexibleHeight=1;
            area.GetComponent<Image>().color=WbCard;return list;
        }
        private static RectTransform WbColumn(Transform parent,string name,float weight=1)
        {
            var panel=Ui.Panel(parent,name,WbCard).rectTransform;Ui.Size(panel,-1,-1,weight);Ui.VList(panel,8,14);return panel;
        }
        private static void WbVital(Transform parent,string label,double value,double max)
        {
            WbText(parent,label+" "+value.ToString("0")+" / "+max.ToString("0"),17);
            var track=Ui.Panel(parent,"Vital_"+label,new Color32(220,225,214,255)).rectTransform;Ui.Size(track,-1,7);
            Ui.Panel(track,"Value",Ui.ButtonHot).rectTransform.Anchor(0,0,(float)Math.Max(0,Math.Min(1,value/Math.Max(1,max))),1);
        }
        private static void WbPriceGraph(Transform parent,WorkbenchPriceDay[] data,float height)
        {
            var rect=Ui.Rect(parent,"WorkbenchPriceTrend");Ui.Size(rect,-1,height);
            rect.gameObject.AddComponent<CanvasRenderer>();
            var graphic=rect.gameObject.AddComponent<WorkbenchHistoryGraphic>();graphic.raycastTarget=false;graphic.Values=data.Select(d=>d.Price).ToArray();
            graphic.color=data.Length>1&&data.Last().Price<data.First().Price?new Color(.3f,.5f,.65f):Ui.Warn;
        }
        private void RefreshWorkbenchAccess()
        {
            if(_workbenchLauncher==null)
            {
                _workbenchLauncher=Ui.Button(_dockCanvas.transform,"居家工作台",OpenHomeWorkbench,17,Ui.ButtonHot);
                _workbenchLauncher.name="HomeWorkbenchLauncher";
                ((RectTransform)_workbenchLauncher.transform).Anchor(1,1,1,1,-232,-108,18,68);
            }
            bool visible=S.CurrentPlace==S.Home&&!_ctrl.Placing&&!S.IsGameOver&&!MainMenuOpen;
            _workbenchLauncher.gameObject.SetActive(visible);
            _workbenchLauncher.interactable=S.CanUseHomeWorkbench;
            _workbenchLauncher.GetComponentInChildren<Text>().text=S.DebugHomeWorkbenchEnabled?"居家工作台 · 测试":S.OwnsHomeWorkbench?"居家工作台":"居家工作台 · 未购大屏";
            if(_homeWorkbench!=null&&!S.CanUseHomeWorkbench)_homeWorkbench.Close();
        }
        public void OpenHomeWorkbench()
        {
            if(!S.CanUseHomeWorkbench){Toast("在家具城购买家庭经营大屏，送达后可在家打开工作台。");return;}
            if(_homeWorkbench!=null)return;
            _homeWorkbench=OpenWindow("居家工作台",1420,800,BuildHomeWorkbench,holds:false,onClosed:()=>_homeWorkbench=null,scrollContent:false);
            _homeWorkbench.LiveConversation=true;
            _homeWorkbench.LiveKey=()=>S.CanUseHomeWorkbench?S.Clock.Now.TotalMinutes+":"+S.Ledger.Revision+":"+S.WorkbenchAlerts.Count+":"+S.Player.Health+":"+S.Player.Hunger:"closed";
            _homeWorkbench.LiveStamp=_homeWorkbench.LiveKey();
        }
        private void BuildHomeWorkbench(Window w)
        {
            if(!S.CanUseHomeWorkbench){w.Close();return;}
            w.Content.GetComponentInParent<Image>().color=WbPaper;
            var title=Ui.Row(w.Content,38,10);title.name="WorkbenchStatusBar";
            Ui.Size(WbText(title,S.Clock.Now+" · 在家",17),-1,38,1);
            Ui.Size(WbText(title,"现金 "+S.Ledger.Balance,20),260,38);
            Ui.Size(WbButton(title,S.Time.IsRunning?"暂停":"继续",()=>{S.Time.TogglePause();w.RequestRebuild();},"WorkbenchPause"),100,38,0);
            Ui.Size(WbButton(title,"生活托管",OpenHomeAutomation,"WorkbenchAutomation"),180,38,0);
            var body=Ui.Rect(w.Content,"WorkbenchBody");var le=Ui.Size(body).GetComponent<LayoutElement>();le.minHeight=160;le.flexibleHeight=1;Ui.VList(body,8,0);
            switch(_workbenchTab)
            {
                case 0:BuildWorkbenchPersonal(body,w);break;
                case 1:BuildWorkbenchBusiness(body,w);break;
                case 2:BuildWorkbenchInvestments(body,w);break;
                case 3:BuildWorkbenchContacts(body);break;
            }
            var nav=Ui.Row(w.Content,46,9);nav.name="WorkbenchNavigation";
            string[] tabs={"总览","经营","投资","联系人"};
            for(int i=0;i<tabs.Length;i++){int index=i;WbButton(nav,tabs[i],()=>{_workbenchTab=index;w.RequestRebuild();},"WorkbenchTab_"+i,_workbenchTab==i);}
        }
        private (GameDate from,GameDate to) WorkbenchDates(bool personal=false)
        {
            int today=S.Clock.Today.TotalDays;
            if(_workbenchPeriod==0&&personal){int day=Math.Max(0,today-1);return(new GameDate(day),new GameDate(day));}
            int first=_workbenchPeriod==2?today/30*30:_workbenchPeriod==1?Math.Max(0,today-6):today;
            return(new GameDate(first),S.Clock.Today);
        }
        private void WorkbenchPeriods(Transform parent,Window w,bool personal)
        {
            var row=Ui.Row(parent,34,6);string[] names={personal?"昨日":"今日","近7日","本月"};
            for(int i=0;i<names.Length;i++){int n=i;WbButton(row,names[i],()=>{_workbenchPeriod=n;w.RequestRebuild();},"WorkbenchPeriod_"+i,_workbenchPeriod==i);}
        }
        private void BuildWorkbenchPersonal(Transform parent,Window w)
        {
            var row=Ui.Row(parent,-1,14);Ui.Size(row,-1,-1,1).GetComponent<LayoutElement>().flexibleHeight=1;
            var me=WbColumn(row,"WorkbenchPersonalState");
            WbText(me,"我的状态",22);WbVital(me,"健康",S.Player.Health,100);WbVital(me,"精力",S.Player.Energy,S.Player.MaxEnergy);WbVital(me,"饱腹",S.Player.Hunger,100);
            WbText(me,"心情 "+S.Player.Mood.ToString("0")+" · 营养 "+S.Player.Nutrition.ToString("0"),17);
            WbText(me,S.HomeTitle,20);WbText(me,"月租 "+S.CurrentHomeRent,17,WbDim);
            WbText(me,"载具："+S.CurrentVehicle.Name,18);WbText(me,"过去24小时睡眠 "+S.SleepHoursLast24.ToString("0.#")+" / 8 小时",17);
            WbButton(me,"生活托管设置",OpenHomeAutomation,"WorkbenchPersonalAutomation");
            WbButton(me,"背包与家中物品",OpenBackpackPanel,"WorkbenchBackpack");
            var finance=WbColumn(row,"WorkbenchPersonalFinance",1.1f);
            WbText(finance,"我的财务",22);WbText(finance,"现金 "+S.Ledger.Balance,25);WbText(finance,"净资产 "+S.NetWorth(),19);WbText(finance,"债务 "+S.TotalDebt,18);
            WorkbenchPeriods(finance,w,true);var range=WorkbenchDates(true);var flow=S.WorkbenchPersonalCash(range.from,range.to);
            if(_workbenchPeriod==0&&S.Clock.Today.TotalDays==0)flow=(Money.Zero,Money.Zero);
            WbText(finance,"收入 +"+flow.income,21);WbText(finance,"支出 "+flow.expense,21);WbText(finance,"日常收支 · 投资与借还单列",14,WbDim);
            WbButton(finance,"完整账本",OpenLedger,"WorkbenchLedger");WbButton(finance,"物业与资产",OpenCityProperties,"WorkbenchProperties");
            WbButton(finance,"债务与还款",()=>{_businessTab=0;ShowBusinessPanel(false);},"WorkbenchDebt");
            var alerts=WbColumn(row,"WorkbenchPersonalDecisions");WbText(alerts,"需要我决定",22);
            var list=WbScroll(alerts,"WorkbenchDecisionsList");
            if(S.PendingEvent!=null)WbButton(list,S.PendingEvent.Title,()=>ShowEvent(S.PendingEvent),"WorkbenchPendingEvent");
            foreach(var alert in S.WorkbenchAlerts)WbText(list,alert.Text,17,Ui.Warn);
            var offered=S.CityActivities.Where(a=>a.State==CityActivityPhase.Offered).ToArray();
            foreach(var activity in offered){string id=activity.Id;WbButton(list,"待回复邀约 · "+(CityLifeCatalog.Activities.FirstOrDefault(d=>d.Id==activity.DefinitionId)?.Name??activity.DefinitionId),()=>OpenCityActivityDetail(id),"WorkbenchInvitation_"+id);}
            if(S.PendingEvent==null&&S.WorkbenchAlerts.Count==0&&offered.Length==0)WbText(list,"暂无待处理事项",18,WbDim);
            foreach(var activity in S.CityActivities.Where(a=>a.State==CityActivityPhase.Scheduled||a.State==CityActivityPhase.Gathering).OrderBy(a=>a.ScheduledStart).Take(3))
            {string id=activity.Id;WbButton(list,"日程 · "+new GameTimestamp(activity.ScheduledStart),()=>OpenCityActivityDetail(id),"WorkbenchUpcoming_"+id);}
            WbButton(alerts,"日程与邀约",OpenCityActivities,"WorkbenchCalendar");
            WbButton(alerts,"手机消息",OpenChatPanel,"WorkbenchMessages");
        }
        private void BuildWorkbenchBusiness(Transform parent,Window w)
        {
            var nav=Ui.Row(parent,38,6);string[] names={"报表","门店","人员采购","订单合作","资产"};
            for(int i=0;i<names.Length;i++){int index=i;WbButton(nav,names[i],()=>{_workbenchBusinessTab=index;w.RequestRebuild();},"WorkbenchBusiness_"+i,_workbenchBusinessTab==i);}
            if(_workbenchBusinessTab==0){BuildWorkbenchReport(parent,w);return;}
            if(_workbenchBusinessTab==1){BuildWorkbenchShops(parent,w);return;}
            var list=WbScroll(parent,"WorkbenchBusinessFunctions");
            if(_workbenchBusinessTab==2)
            {
                WbButton(list,"员工 · 岗位 / 排班 / 培训",OpenEmployeesPanel,"WorkbenchEmployees");
                WbButton(list,"统一采购与补货责任",OpenCityProcurement,"WorkbenchProcurement");
                foreach(var shop in S.MyShops){var target=shop;WbButton(list,S.ShopTitle(shop)+" · 进货员与采购记录",()=>OpenShopBuyers(target),"WorkbenchBuyers_"+shop.Place.Id);}
            }
            else if(_workbenchBusinessTab==3)
            {
                WbText(list,"已解锁的业务",20);
                if(BusinessUnlocked)
                {
                    WbButton(list,"直供采购",()=>{_businessTab=1;ShowBusinessPanel(false);},"WorkbenchDirectSupply");
                    WbButton(list,"企业合同",()=>{_businessTab=2;ShowBusinessPanel(false);},"WorkbenchContracts");
                    WbButton(list,"推广合作",()=>{_businessTab=4;ShowBusinessPanel(false);},"WorkbenchPromotion");
                }
                else WbText(list,"经营成长后解锁直供、企业合同和推广合作。",18,WbDim);
                foreach(var shop in S.MyShops){var target=shop;WbButton(list,S.ShopTitle(shop)+" · 装机订单 "+shop.Sim.Orders.Count+" 单",()=>OpenWorkbenchOrders(target),"WorkbenchOrders_"+shop.Place.Id);}
            }
            else
            {
                WbButton(list,"具体物业、租约与收租",OpenCityProperties,"WorkbenchAssets");
                WbButton(list,"车辆与占用",OpenFleetPanel,"WorkbenchVehicles");
                if(BusinessUnlocked)WbButton(list,"物业与住处账目",()=>{_businessTab=5;ShowBusinessPanel(false);},"WorkbenchPropertyAccounts");
                if(S.HasPark)WbButton(list,"园区管理",()=>{_businessTab=6;ShowBusinessPanel(false);},"WorkbenchPark");
            }
        }
        private void BuildWorkbenchReport(Transform parent,Window w)
        {
            WorkbenchPeriods(parent,w,false);var range=WorkbenchDates();var summary=S.GetCityFinance(range.from,range.to);
            WbText(parent,"销售 "+summary.Sales+"    经营支出 "+summary.OperatingOut+"    现金净额 "+summary.OperatingNet,22).name="WorkbenchOperatingSummary";
            var row=Ui.Row(parent,-1,15);Ui.Size(row,-1,-1,1).GetComponent<LayoutElement>().flexibleHeight=1;
            var report=WbColumn(row,"WorkbenchShopReports",1.3f);WbText(report,"门店收支",21);
            var list=WbScroll(report,"WorkbenchShopReportList");
            foreach(var shop in S.MyShops)
            {
                var result=S.GetCityFinance(range.from,range.to,shop.Place.Id);
                WbText(list,S.ShopTitle(shop)+" · "+(shop.Sim.IsOpen?"营业":"休息"),20);
                WbText(list,"销售 "+result.Sales+" / 支出 "+result.OperatingOut+" / 净额 "+result.OperatingNet,17);
            }
            WbText(report,"现金净额反映经营收付，不等于库存销售利润。",14,WbDim);
            var market=WbColumn(row,"WorkbenchCategoryPrices");WbText(market,"品类进货行情",21);
            var prices=WbScroll(market,"WorkbenchCategoryPriceList");
            var kinds=S.MyShops.Select(s=>s.RetailKind).Concat(new[]{"clothing","models","computer"}).Distinct().Where(k=>S.WorkbenchPrices(k).Length>0).ToArray();
            foreach(string kind in kinds)
            {
                string id=kind;var history=S.WorkbenchPrices(id);double? change=S.WorkbenchPriceChange(id);
                int span=history.Length>1?Math.Min(7,history.Last().Day-history.First().Day):0;
                WbText(prices,RetailShopTypes.Name(id)+(S.MyShops.Any(s=>s.RetailKind==id)?" · 经营中":" · 关注"),18);
                WbPriceGraph(prices,history,44);
                WbButton(prices,change.HasValue?"近"+span+"日 "+change.Value.ToString("+0.0;-0.0;0.0")+"%":"历史记录中 · "+history.Length+" 日",()=>OpenWorkbenchPriceHistory(id),"WorkbenchPrice_"+id);
            }
        }
        private void OpenWorkbenchPriceHistory(string kind)
        {
            OpenWindow(RetailShopTypes.Name(kind)+" · 进货价格历史",820,610,w=>
            {
                var data=S.WorkbenchPrices(kind);Line(w.Content,"品类平均进货价 · 首个记录日为100",20,Ui.Accent);
                WbPriceGraph(w.Content,data,100);
                if(data.Length<2)Para(w.Content,"从实际游戏日期开始记录；不足两日时不生成虚构涨跌。",18,Ui.Dim);
                double first=data.FirstOrDefault()?.Price??0;
                foreach(var sample in data.Reverse())Line(w.Content,new GameDate(sample.Day)+" · 指数 "+(first<=0?"—":(sample.Price/first*100).ToString("0.00")),18);
                Para(w.Content,"分类数据来自实际进货价；平价商品可能保持不变。具体供货与配额以供应商为准。",16,Ui.Dim);
            });
        }
        private void BuildWorkbenchShops(Transform parent,Window w)
        {
            var shops=S.MyShops.ToArray();if(shops.Length==0){WbText(parent,"还没有经营中的门店。",20);return;}
            int index=Array.FindIndex(shops,s=>s.Place.Id==_workbenchShop);if(index<0)index=0;var shop=shops[index];_workbenchShop=shop.Place.Id;
            var selector=Ui.Row(parent,38);Ui.Size(Ui.Choice(selector,shops.Select(S.ShopTitle).ToArray(),index,i=>{_workbenchShop=shops[i].Place.Id;w.RequestRebuild();}),-1,38,1);
            if(S.IsOwnedVenue(shop.Place)){WbButton(parent,"服务价格、营业与值守",()=>OpenVenueManagement(shop.Place),"WorkbenchVenue");return;}
            var sim=shop.Sim;var hours=Ui.Row(parent,38,7);
            WbText(hours,"营业时间",17);var options=Enumerable.Range(0,24).Select(i=>i.ToString("00")+":00").ToArray();
            var open=Ui.Size(Ui.Choice(hours,options,sim.AllDay?9:sim.OpenMinute/60,i=>{S.SetWorkbenchHours(shop.Place.Id,i,sim.AllDay?21:sim.CloseMinute/60%24,false);w.RequestRebuild();}),150,38);open.name="WorkbenchOpenHour";
            var close=Ui.Size(Ui.Choice(hours,options,sim.AllDay?21:sim.CloseMinute/60%24,i=>{S.SetWorkbenchHours(shop.Place.Id,sim.AllDay?9:sim.OpenMinute/60,i,false);w.RequestRebuild();}),150,38);close.name="WorkbenchCloseHour";
            WbButton(hours,sim.AllDay?"不限时 ✓":"不限时",()=>{S.SetWorkbenchHours(shop.Place.Id,9,21,!sim.AllDay);w.RequestRebuild();},"WorkbenchAllDay");
            WbText(parent,"统一加价 "+sim.Markup.ToString("0.00")+"×",19);
            Ui.Slider(parent,.9f,1.6f,(float)sim.Markup,v=>{S.SetWorkbenchMarkup(shop.Place.Id,Math.Round(v*20)/20);w.RequestRebuild();}).name="WorkbenchMarkup";
            var actions=Ui.Row(parent,40,8);WbButton(actions,"员工与排班",OpenEmployeesPanel,"WorkbenchShopStaff");WbButton(actions,"进货员与记录",()=>OpenShopBuyers(shop),"WorkbenchShopBuyers");
            var list=WbScroll(parent,"WorkbenchDisplayedProducts");WbText(list,"已陈列商品 · 定价",20);
            foreach(var kind in ShelfKinds.All)
            {
                var fill=sim.ShelfFillOf(kind);
                foreach(var line in fill.Goods)
                {
                    string id=line.Key;WbButton(list,Gifts.Get(id).Name+" ×"+line.Value+" · "+sim.GoodsPriceOf(id)+" · 购买意愿 "+(sim.GoodsPurchaseIntentAtPrice(id,sim.GoodsPriceOf(id).Yuan)*100).ToString("0")+"%",
                        ()=>OpenWorkbenchGoodsPrice(shop,id),"WorkbenchGoods_"+id);
                }
                foreach(var line in fill.Parts)
                {
                    var part=line.Part;bool boxed=line.Boxed;WbButton(list,part.Name+(boxed?" 盒装":" 散件")+" ×"+line.Count+" · "+sim.PriceOf(part,boxed),
                        ()=>OpenWorkbenchPartPrice(shop,part,boxed),"WorkbenchPart_"+part.Id+(boxed?"_b":"_l"));
                }
            }
            WbText(list,"后仓库存（查看）",20);
            foreach(var line in sim.Stock.GoodsLines())WbText(list,Gifts.Get(line.Key).Name+" · 总库存 "+line.Value+"，陈列 "+sim.OnShelfGoods(line.Key),16);
            foreach(var line in sim.Stock.Lines())WbText(list,line.Part.Name+(line.Boxed?" 盒装":" 散件")+" · 总库存 "+line.Count,16);
            WbText(list,"搬运、摆放家具和手工装机需本人到店。",15,WbDim);
        }
        private void OpenWorkbenchGoodsPrice(OwnedShop shop,string id)
        {
            OpenWindow("调价 · "+Gifts.Get(id).Name,650,280,w=>
            {
                if(!S.CanUseHomeWorkbench||shop.Rented!=true||shop.Sim.OnShelfGoods(id)<=0){w.Close();return;}
                var sim=shop.Sim;Ui.Size(ShelfSaleTag(w,w.Content,"goods_"+id,sim.GoodsPriceOf(id).Yuan,sim.GoodsPriceRange(id),v=>sim.GoodsPurchaseIntentAtPrice(id,v),
                    v=>{if(!S.SetWorkbenchGoodsPrice(shop.Place.Id,id,v))Toast("商品已取消陈列或当前不能远程调价。");_homeWorkbench?.RequestRebuild();},sim),-1,140);
            });
        }
        private void OpenWorkbenchOrders(OwnedShop shop)
        {
            OpenWindow(S.ShopTitle(shop)+" · 订单与装机",900,650,w=>
            {
                if(!S.CanUseHomeWorkbench||shop.Rented!=true){w.Close();return;}
                Line(w.Content,"自动装机："+(shop.AutoBuild?"开":"关")+" · 订单 "+shop.Sim.Orders.Count+" 单",21,Ui.Accent);
                Ui.Size(Ui.Button(w.Content,"切换员工自动装机",()=>{S.ToggleAutoBuildAt(shop);w.Rebuild();},18),-1,40).name="WorkbenchAutoBuild_"+shop.Place.Id;
                foreach(var order in shop.Sim.Orders)
                {
                    Line(w.Content,PartCatalog.TierName(order.Tier)+"整机 · "+order.Price+" · 截止 "+order.DueAt,20);
                    Line(w.Content,order.PartsReserved?"配件已领用 · 员工剩余 "+order.EmployeeMinutesLeft+" 分钟":"等待配件与员工处理",17);
                    foreach(var part in PartCatalog.BuildList(order.Tier))Line(w.Content,part.Name+" · "+(order.PartsReserved||shop.Sim.Stock.CountAll(part)>0?"已备":"缺货"),16,Ui.Dim);
                }
                if(shop.Sim.Orders.Count==0)Line(w.Content,"当前没有装机订单。",18,Ui.Dim);
                Para(w.Content,"员工按真实岗位、在岗位置和配件处理订单。手工装机需本人到店。",16,Ui.Dim);
            });
        }
        private void OpenWorkbenchPartPrice(OwnedShop shop,PartDef part,bool boxed)
        {
            OpenWindow("调价 · "+part.Name,650,280,w=>
            {
                if(!S.CanUseHomeWorkbench||shop.Rented!=true){w.Close();return;}var sim=shop.Sim;
                Ui.Size(ShelfSaleTag(w,w.Content,"workbench_"+part.Id,sim.PriceOf(part,boxed).Yuan,sim.PartPriceRange(part,boxed),v=>sim.PartPurchaseIntentAtPrice(part,boxed,v),
                    v=>{if(!S.SetWorkbenchPartPrice(shop.Place.Id,part.Id,boxed,v))Toast("商品已取消陈列或当前不能远程调价。");_homeWorkbench?.RequestRebuild();},sim),-1,140);
            });
        }
        private void BuildWorkbenchInvestments(Transform parent,Window w)
        {
            var reserve=S.GetCityCashReserve();WbText(parent,"投资市值 "+S.CityEquityValue+" · 已实现 "+S.CityEquityRealized,24);
            WbText(parent,"现金 "+S.Ledger.Balance+" · 经营预留 "+reserve.Total+" · 预留后 "+reserve.Available,20);
            WbButton(parent,"行情、持仓与真实买卖",OpenCityInvestments,"WorkbenchTrade",true);
            var list=WbScroll(parent,"WorkbenchInvestmentNews");WbText(list,S.CityInvestmentNews,18);
            WbButton(list,"资产与负债",()=>{_businessTab=0;ShowBusinessPanel(false);},"WorkbenchInvestmentDebt");
        }
        private void BuildWorkbenchContacts(Transform parent)
        {
            var actions=Ui.Row(parent,42,10);WbButton(actions,"通讯录与邀约",OpenContactsPanel,"WorkbenchContacts");WbButton(actions,"聊天消息",OpenChatPanel,"WorkbenchChat");WbButton(actions,"日程",OpenCityActivities,"WorkbenchContactCalendar");
            WbText(parent,"使用已有联系方式，沿用真实NPC的聊天与日程。",17,WbDim);
            var list=WbScroll(parent,"WorkbenchContactsBody");
            foreach(var contact in S.SavedConversationContacts())
            {
                string id=contact.Id;WbButton(list,contact.Name+" · "+contact.Role,()=>OpenPersonDialogue(id,remote:true),"WorkbenchKnownContact_"+id);
            }
            foreach(var worker in S.Employees)
            {
                string personId=S.PersonIdOf(worker.Person);var employee=worker;
                WbButton(list,employee.Person.Name+" · "+GameSession.RoleName(employee.Role)+" · "+S.EmployeeStatus(employee),()=>OpenPersonDialogue(personId,remote:true),"WorkbenchContact_"+employee.Person.Id,enabled:S.RemoteConversationBlocker(personId)==null);
            }
        }
        private void OpenHomeAutomation()
        {
            if(!S.CanUseHomeWorkbench)return;
            OpenWindow("生活托管",830,700,w=>
            {
                if(!S.CanUseHomeWorkbench){w.Close();return;}
                var current=S.HomeAutomation;
                HomeAutomationSettings Copy(){var a=S.HomeAutomation;return new HomeAutomationSettings{Meals=a.Meals,Takeaway=a.Takeaway,Sleep=a.Sleep,Clothing=a.Clothing,Supplies=a.Supplies,SleepHour=a.SleepHour,WakeHour=a.WakeHour,MealTier=a.MealTier,DailyMealBudget=a.DailyMealBudget,SupplyId=a.SupplyId};}
                void Apply(Action<HomeAutomationSettings> edit){var next=Copy();edit(next);if(S.SetHomeAutomation(next)){w.RequestRebuild();_homeWorkbench?.RequestRebuild();}}
                void Toggle(string key,string title,bool enabled,Action<HomeAutomationSettings> edit)
                {Ui.Size(Ui.Button(w.Content,title+" · "+(enabled?"开 ✓":"关"),()=>Apply(edit),18,enabled?Ui.ButtonHot:Ui.ButtonBg),-1,40).name="Automation_"+key;}
                Toggle("Meals","自动吃饭 · 先用已有饭食",current.Meals,a=>a.Meals=!a.Meals);
                Toggle("Takeaway","自动点饭 · 食物不足时",current.Takeaway,a=>a.Takeaway=!a.Takeaway);
                var meal=Ui.Row(w.Content,38,7);Line(meal,"外卖餐档",17);
                Ui.Size(Ui.Choice(meal,GameSession.DeliveryMeals.Select(m=>m.Name+" ¥"+m.Price).ToArray(),current.MealTier,i=>Apply(a=>a.MealTier=i)),-1,38,1).name="AutomationMealTier";
                var budgets=new[]{0,25,50,60,100,200,600,1600,5000,current.DailyMealBudget}.Distinct().OrderBy(n=>n).ToArray();
                Line(meal,"每日预算",17);Ui.Size(Ui.Choice(meal,budgets.Select(n=>"¥"+n).ToArray(),Array.IndexOf(budgets,current.DailyMealBudget),i=>Apply(a=>a.DailyMealBudget=budgets[i])),-1,38,1).name="AutomationBudget";
                Toggle("Sleep","自动睡觉 · 需要床",current.Sleep,a=>a.Sleep=!a.Sleep);
                var sleep=Ui.Row(w.Content,38,7);var hours=Enumerable.Range(0,24).Select(i=>i.ToString("00")+":00").ToArray();
                Line(sleep,"入睡",17);Ui.Size(Ui.Choice(sleep,hours,current.SleepHour,i=>Apply(a=>a.SleepHour=i)),150,38);Line(sleep,"起床",17);Ui.Size(Ui.Choice(sleep,hours,current.WakeHour,i=>Apply(a=>a.WakeHour=i)),150,38);
                Toggle("Clothing","自动换衣 · 使用已有衣服与衣柜",current.Clothing,a=>a.Clothing=!a.Clothing);
                Toggle("Supplies","自动取用 · 后仓饭食及每日消耗品",current.Supplies,a=>a.Supplies=!a.Supplies);
                string[] supply={Gifts.Fruit,Gifts.Snack,Gifts.Soda};Ui.Size(Ui.Choice(w.Content,supply.Select(id=>Gifts.Get(id).Name).ToArray(),Array.IndexOf(supply,current.SupplyId),i=>Apply(a=>a.SupplyId=supply[i])),-1,38);
                Line(w.Content,"已有可用饭食 "+S.WorkbenchMealsAvailable+" 份 · 今日点饭已花 ¥"+S.WorkbenchMealSpent,18,Ui.Accent);
                Para(w.Content,"只在家且拥有大屏时执行。睡觉正常推进时间；衣服和床不按日消耗。缺货、缺设施或预算不足会提醒，同一未解决问题不反复弹窗。",16,Ui.Dim);
            });
        }
    }
}
