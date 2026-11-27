using System;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Session;
using PCShopSell.Core.Shop;
using PCShopSell.Core.Time;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private int WorkbenchTodoCount() => (S.PendingEvent != null ? 1 : 0)
            + S.AppLoans.Count(l => !l.Repaid && l.DueDay - S.Clock.Today.TotalDays <= 7)
            + StockIds.Count(id => id != "hardware" && S.StockState(id)?.ShortUnits > 0 && S.StockMarginRatio(id) < 1.5)
            + S.WorkbenchAlerts.Count + S.MyShops.Count(s => !S.IsOwnedVenue(s.Place) && WbStockCount(s) == 0)
            + S.CityActivities.Count(a => a.State == CityActivityPhase.Offered)
            + S.CityActivities.Count(a => a.State == CityActivityPhase.Scheduled || a.State == CityActivityPhase.Gathering);

        private void BuildWorkbenchUnifiedShop(Transform parent, Window w, OwnedShop shop)
        {
            var columns = Ui.Row(parent, -1, 16); Ui.Size(columns).GetComponent<LayoutElement>().flexibleHeight = 1;
            var middle = WbColumn(columns, "WorkbenchShopSettings", .9f);
            var right = WbColumn(columns, "WorkbenchShopPricing", 1.4f);
            var sim = shop.Sim;
            WbHeading(middle, S.ShopTitle(shop), 21);
            var badges = Ui.Row(middle, 24, 8); WbChip(badges, sim.IsOpen ? "营业中" : "休息", sim.IsOpen ? 'g' : 'n');
            if (!S.IsOwnedVenue(shop.Place) && WbStockCount(shop) == 0) WbChip(badges,"缺货",'r');
            var settings = WbScroll(middle, "WorkbenchShopSettingsList");
            RectTransform Section(string name,string title)
            {
                var panel=Ui.FlatPanel(settings,name,Ui.ButtonBg).rectTransform;Ui.VList(panel,5,10);WbHeading(panel,title,17);return panel;
            }
            var overview=Section("WorkbenchShopToday","今日概况");
            WbStat(overview,"今日收入",S.GetCityFinance(S.Clock.Today,S.Clock.Today,shop.Place.Id).Sales.ToString(),null,15);
            WbStat(overview,"近7日收支差额",S.GetCityFinance(new GameDate(Math.Max(0,S.Clock.Today.TotalDays-6)),S.Clock.Today,shop.Place.Id).OperatingNet.ToString(),null,15);
            int tasks=S.ShopEventTasks.Count(t=>t.ShopId==shop.Place.Id);
            WbStat(overview,"待处理",tasks>0?tasks+" 项事件":WbStockCount(shop)==0?"库存已空":"暂无",null,15);
            if (S.IsOwnedVenue(shop.Place))
            {
                WbLink(settings,"服务价格、营业与值守",()=>OpenVenueManagement(shop.Place),"WorkbenchVenue");
                WbHeading(right,"服务场所");WbText(right,"经营设置与员工值守",17);
                WbLink(right,"打开场所管理",()=>OpenVenueManagement(shop.Place),"WorkbenchVenueDetails");return;
            }
            var operations=Section("WorkbenchShopOperations","经营设置");
            var hours=Ui.Row(operations,34,6);
            var options=Enumerable.Range(0,24).Select(i=>i.ToString("00")+":00").ToArray();
            int openHour=sim.AllDay?ShopSimulation.DefaultOpenMinute/60:sim.OpenMinute/60;
            int closeHour=sim.AllDay?ShopSimulation.DefaultCloseMinute/60:sim.CloseMinute/60%24;
            Ui.Size(Ui.Choice(hours,options,openHour,i=>{S.SetWorkbenchHours(shop.Place.Id,i,closeHour,false);w.RequestRebuild();}),86,32).name="WorkbenchOpenHour";
            Ui.Size(Ui.Choice(hours,options,closeHour,i=>{S.SetWorkbenchHours(shop.Place.Id,openHour,i,false);w.RequestRebuild();}),86,32).name="WorkbenchCloseHour";
            Ui.Size(Ui.Button(hours,sim.AllDay?"不限时 ✓":"不限时",()=>{S.SetWorkbenchHours(shop.Place.Id,openHour,closeHour,!sim.AllDay);w.RequestRebuild();},14),0,32,1).name="WorkbenchAllDay";
            var markup=Ui.Row(operations,34,6);Ui.Size(Ui.Label(markup,"统一加价",15),0,32,1);
            void Markup(double delta){S.SetWorkbenchMarkup(shop.Place.Id,Math.Max(ShopSettings.MinMarkup,Math.Min(ShopSettings.MaxMarkup,sim.Markup+delta)));w.RequestRebuild();}
            Ui.Size(Ui.Button(markup,"−",()=>Markup(-.05),19),30,30).name="WorkbenchMarkupMinus_"+shop.Place.Id;
            Ui.Size(Ui.Label(markup,sim.Markup.ToString("0.00")+"×",16,TextAnchor.MiddleCenter),66,30).name="WorkbenchMarkup_"+shop.Place.Id;
            Ui.Size(Ui.Button(markup,"+",()=>Markup(.05),19),30,30).name="WorkbenchMarkupPlus_"+shop.Place.Id;
            var team=Section("WorkbenchShopTeam","员工与进货员");
            var staff=S.Employees.Where(e=>e.Shop==shop).ToArray();
            WbText(team,"员工 "+staff.Length+" 人 · 进货员 "+(staff.Any(e=>e.Role==EmployeeRole.Buyer)?"已安排":"未指定"),15);
            foreach(var employee in staff) WbText(team,employee.Person.Name+" · "+GameSession.RoleName(employee.Role)+" · "+S.EmployeeStatus(employee),14,WbDim);
            WbLink(team,"员工与排班",OpenEmployeesPanel,"WorkbenchShopStaff",32);
            WbLink(team,"进货员与记录",()=>OpenShopBuyers(shop),"WorkbenchShopBuyers",32);
            var receipt=S.LatestShopPurchaseReceipt(shop.Place.Id);
            WbText(team,"最近到货 · "+ArrivalText(receipt),14,WbDim);
            var orders=Section("WorkbenchShopOrders","订单与装机");
            Ui.Size(Ui.Button(orders,"自动装机  "+(shop.AutoBuild?"开 ✓":"关"),()=>{S.ToggleAutoBuildAt(shop);w.RequestRebuild();},15),-1,32).name="WorkbenchAutoBuild_"+shop.Place.Id;
            WbLink(orders,"订单 "+sim.Orders.Count+" 单  ›",()=>OpenWorkbenchOrders(shop),"WorkbenchOrders_"+shop.Place.Id,32);

            WbHeading(right,"货架与定价",21);
            var heading=Ui.Row(right,30,8);Ui.Size(Ui.Label(heading,"商品",14,color:WbDim),0,30,1);
            Ui.Size(Ui.Label(heading,"陈列 / 库存",14,TextAnchor.MiddleRight,WbDim),84,30);
            Ui.Size(Ui.Label(heading,"售价",14,TextAnchor.MiddleCenter,WbDim),154,30);
            Ui.Size(Ui.Label(heading,"购买欲望",14,TextAnchor.MiddleRight,WbDim),78,30);
            var shelf=WbScroll(right,"WorkbenchShelfPriceList");
            int count=0;
            foreach(var kind in ShelfKinds.All)
            {
                var fill=sim.ShelfFillOf(kind);
                foreach(var line in fill.Goods)
                {
                    string id=line.Key;int total=sim.Stock.GoodsLines().Where(l=>l.Key==id).Sum(l=>l.Value);
                    WorkbenchPriceRow(shelf,w,Gifts.Get(id).Name,line.Value+" / "+total,"WorkbenchGoods_"+id,
                        ()=>sim.GoodsPriceOf(id).Yuan,sim.GoodsPriceRange(id),v=>sim.GoodsPurchaseEstimateAtPrice(id,v),v=>S.SetWorkbenchGoodsPrice(shop.Place.Id,id,v));count++;
                }
                foreach(var line in fill.Parts)
                {
                    var part=line.Part;bool boxed=line.Boxed;
                    WorkbenchPriceRow(shelf,w,part.Name+(boxed?"·盒":"·散"),line.Count+" / "+sim.Stock.CountAll(part),"WorkbenchPart_"+part.Id+(boxed?"_b":"_l"),
                        ()=>sim.PriceOf(part,boxed).Yuan,sim.PartPriceRange(part,boxed),v=>sim.PartPurchaseIntentAtPrice(part,boxed,v),v=>S.SetWorkbenchPartPrice(shop.Place.Id,part.Id,boxed,v));count++;
                }
            }
            if(count==0) WbText(shelf,"尚未陈列商品",17,WbDim);
            var back=sim.Stock.GoodsLines().Select(l=>Gifts.Get(l.Key).Name+" "+l.Value).Concat(sim.Stock.Lines().Select(l=>l.Part.Name+" "+l.Count)).ToArray();
            var warehouse=WbScroll(right,"WorkbenchWarehouse");Ui.Size(warehouse.parent.parent.GetComponent<RectTransform>(),-1,72).GetComponent<LayoutElement>().flexibleHeight=0;
            WbText(warehouse,"后仓库存："+(back.Length==0?"空":string.Join(" / ",back)),14,WbDim);
            var purchase=Ui.Row(right,46,8);WbFlex(purchase);
            WbAction(purchase,"本店采购",()=>OpenCityProcurementFor(shop),"WorkbenchShopProcurement",170,true,44);
        }

        private void WorkbenchPriceRow(Transform parent,Window w,string title,string stock,string name,Func<long> price,
            (long min,long max) range,Func<long,double> intent,Func<long,bool> apply)
        {
            var row=Ui.Row(parent,66,8);row.name=name;WbDivider(row);
            Ui.Size(Bold(Ui.Label(row,title,16)),0,-1,1);Ui.Size(Ui.Label(row,stock,15,TextAnchor.MiddleRight),84,-1);
            var step=Ui.Row(row,34,3);Ui.Size(step,154,34,0);
            step.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight=false;
            Text value=null,intentText=null;Image bar=null;
            void Refresh(){long current=price();value.text="¥"+current.ToString("N0");double chance=intent(current);intentText.text=chance>=.65?"高":chance>=.35?"中":"低";bar.color=chance>=.65?Ui.Good:chance>=.35?Ui.ButtonHot:Ui.Warn;bar.rectTransform.anchorMax=new Vector2((float)Math.Max(.05,Math.Min(1,chance)),1);}
            void Change(int direction){long current=price();long next=Math.Max(range.min,Math.Min(range.max,current+direction));if(next!=current&&apply(next)){Refresh();}else if(next!=current)w.RequestRebuild();}
            var minus=Ui.Size(Ui.Button(step,"−",()=>Change(-1),19),30,32);minus.name=name+"_Minus";
            value=Ui.Size(Ui.Label(step,"",16,TextAnchor.MiddleCenter),88,32);value.name=name+"_Price";
            var plus=Ui.Size(Ui.Button(step,"+",()=>Change(1),19),30,32);plus.name=name+"_Plus";
            foreach(var b in new[]{minus,plus}){var repeat=b.GetComponent<HoldRepeat>();repeat.Owner=w;repeat.Key=name+b.name;}
            var desire=Ui.Row(row,26,5);Ui.Size(desire,78,26,0);
            desire.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight=false;
            var track=Ui.Panel(desire,"IntentTrack",Ui.FlatBand);Ui.Size(track,0,5,1);bar=Ui.Panel(track.transform,"Intent",Ui.Good);bar.rectTransform.Fill();bar.raycastTarget=false;
            intentText=Ui.Size(Ui.Label(desire,"",14,TextAnchor.MiddleRight),20,24);Refresh();
        }

        private void OpenWorkbenchCooperation()
        {
            OpenWindow("品牌与合作",700,480,w=>
            {
                if(S.BrandManagementUnlocked)WbLink(w.Content,"品牌 · 代言 / 展会 / 竞标",OpenBrandOverview,"WorkbenchBrandOverview");
                if(BusinessUnlocked)
                {
                    WbLink(w.Content,"直供采购",()=>{_businessTab=1;ShowBusinessPanel(false);},"WorkbenchDirectSupply");
                    if(!GameSession.FirstChapterTrial)
                    {
                        WbLink(w.Content,"企业合同",()=>{_businessTab=2;ShowBusinessPanel(false);},"WorkbenchContracts");
                        WbLink(w.Content,"MCN工作室",()=>{_businessTab=4;ShowBusinessPanel(false);},"WorkbenchPromotion");
                    }
                }
                else WbText(w.Content,"经营成长后解锁合作业务。",17,WbDim);
            });
        }

        private void BuildWorkbenchFinance(Transform parent,Window w)
        {
            var strip=Ui.Row(parent,78,12);strip.name="WorkbenchAssetSummary";
            WbStripCard(strip,"现金",S.Ledger.Balance.ToString());WbStripCard(strip,"净资产",S.NetWorth().ToString());
            WbStripCard(strip,"物业估值",S.PropertyValue.ToString());WbStripCard(strip,"债务",S.TotalDebt.ToString(),S.TotalDebt.IsPositive?Ui.Warn:WbInk);
            var row=Ui.Row(parent,-1,16);Ui.Size(row).GetComponent<LayoutElement>().flexibleHeight=1;
            var flow=WbColumn(row,"WorkbenchPersonalFinance",.85f);WbHeading(flow,"收支");
            var segments=Ui.Row(flow,34,6);WbSegment(segments,new[]{"昨日","近7日","本月"},_workbenchPeriod,n=>{_workbenchPeriod=n;w.RequestRebuild();},"WorkbenchPeriod_",100,32);
            var range=WorkbenchDates(true);var cash=S.WorkbenchPersonalCash(range.from,range.to);
            if(_workbenchPeriod==0&&S.Clock.Today.TotalDays==0)cash=(Money.Zero,Money.Zero);
            WbStat(flow,"收入","+"+cash.income,Ui.Good);WbStat(flow,"支出","−"+cash.expense,Ui.Warn);
            WbStat(flow,"证券浮动盈亏",StockUnrealized.ToString(),StockUnrealized.IsNegative?Ui.Warn:Ui.Good);
            var graph=Ui.Rect(flow,"WorkbenchCashBars");Ui.Size(graph).GetComponent<LayoutElement>().flexibleHeight=1;
            var bars=graph.gameObject.AddComponent<WorkbenchCashGraphic>();bars.raycastTarget=false;
            int days=Math.Max(1,range.to.TotalDays-range.from.TotalDays+1);bars.Income=new double[days];bars.Expense=new double[days];
            for(int i=0;i<days;i++){var d=new GameDate(range.from.TotalDays+i);var f=S.WorkbenchPersonalCash(d,d);bars.Income[i]=f.income.Yuan;bars.Expense[i]=f.expense.Yuan;}
            var dates=Ui.Row(flow,22,4);for(int i=0;i<Math.Min(7,days);i++){int offset=(int)Math.Round(i*(days-1d)/Math.Max(1,Math.Min(7,days)-1));var d=new GameDate(range.from.TotalDays+offset);Ui.Size(Ui.Label(dates,d.Month+"/"+d.Day,12,TextAnchor.MiddleCenter,WbDim),0,22,1);}
            WbLink(flow,"完整账本",OpenLedger,"WorkbenchLedger",36);
            var right=Ui.Rect(row,"WorkbenchFinanceRight");Ui.Size(right,0,-1,1.2f);Ui.VList(right,12,0);
            var assets=WbColumn(right,"WorkbenchAssetList",1.2f);WbHeading(assets,"资产与住处");
            var list=WbScroll(assets,"WorkbenchAssetRows");
            WbTable(list,new[]{"物业","产权","周租金","押金"},new[]{64f,96f,96f},true);
            foreach(var shop in S.MyShops) WbTable(list,new[]{S.ShopTitle(shop),shop.OwnsProperty?"自有":"租赁",shop.OwnsProperty?"—":shop.Lease?.Rent.ToString()??"—",shop.OwnsProperty?"—":shop.Lease?.Deposit.ToString()??"—"},new[]{64f,96f,96f},height:40);
            WbTable(list,new[]{S.HomeTitle,"住处",S.CurrentHomeRent.ToString(),"—"},new[]{64f,96f,96f},height:40);
            var links=Ui.Row(assets,32,6);WbAction(links,"物业与资产",OpenCityProperties,"WorkbenchProperties",128);
            WbAction(links,"载具 · "+S.CurrentVehicle.Name,OpenFleetPanel,"WorkbenchVehicles",156);
            WbAction(links,"更多资产 ›",()=>OpenWorkbenchAssetLinks(),"WorkbenchAssets",128);
            var debt=WbColumn(right,"WorkbenchDebtList",.8f);WbHeading(debt,"借款与到期");
            var loans=WbScroll(debt,"WorkbenchLoanRows");bool first=true;
            foreach(var loan in S.AppLoans.Where(l=>!l.Repaid).OrderBy(l=>l.DueDay))
            {
                var line=Ui.Row(loans,58,8);int left=loan.DueDay-S.Clock.Today.TotalDays;
                Ui.Size(Ui.Label(line,"借款 #"+loan.Id+" · "+(left<=0?"已到期":left+" 天后到期")+"\n应还 "+S.LoanPayoff(loan),16),0,-1,1);
                WbAction(line,"去还款",()=>OpenLoansApp(showRecords:true),"WorkbenchLoanPay_"+loan.Id,100,first,36);first=false;
            }
            if(first)WbText(loans,"暂无未结清借款",16,WbDim);
            WbLink(debt,"手机借款与还款",OpenLoansApp,"WorkbenchDebt",32);
        }

        private void OpenWorkbenchAssetLinks()=>OpenWindow("物业与资产",700,560,w=>
        {
            WbLink(w.Content,"物业、租约与收租",OpenCityProperties,"WorkbenchPropertyAccounts");
            WbLink(w.Content,"融资总览",OpenCityFinanceOverview,"WorkbenchInvestmentDebt");
            WbLink(w.Content,"车辆与占用",OpenFleetPanel,"WorkbenchVehicles");
            if(S.RacingUnlocked)WbLink(w.Content,"湖区赛车",OpenRacingDestinations,"WorkbenchRacing");
            if(BusinessUnlocked)WbLink(w.Content,"住处租约",OpenCityResidence,"WorkbenchResidence");
            if(S.HasPark)WbLink(w.Content,"园区管理",()=>{_businessTab=6;ShowBusinessPanel(false);},"WorkbenchPark");
        });

        private void BuildWorkbenchContacts(Transform parent)
        {
            var row=Ui.Row(parent,-1,16);Ui.Size(row).GetComponent<LayoutElement>().flexibleHeight=1;
            var contacts=WbColumn(row,"WorkbenchContactsBody",1.38f);
            var head=Ui.Row(contacts,36,10);Ui.Size(Bold(Ui.Label(head,"通讯录",21)),0,34,1);
            var input=Ui.Size(Ui.TextInput(head,_wbContactSearch,"搜索姓名",null),210,34);input.name="WorkbenchContactSearch";
            var list=WbScroll(contacts,"WorkbenchContactList");
            void Populate()
            {
                Ui.Clear(list);bool highlighted=false;
                foreach(var contact in S.SavedConversationContacts().Where(c=>string.IsNullOrEmpty(_wbContactSearch)||c.Name.IndexOf(_wbContactSearch,StringComparison.OrdinalIgnoreCase)>=0))
                {
                    string id=contact.Id;
                    var person=PersonSummary(list,id,PersonNameWithKolBadge(id,contact.Name),contact.Role,64,()=>OpenCityPersonProfile(id),ink:WbInk,muted:WbDim);
                    person.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight=false;
                    WbChip(person,Npcs.LevelName(S.Relation(id))+" "+S.Relation(id));
                    bool urgent=!highlighted&&id=="landlady"&&
                        (S.EventBills.Any(b=>!b.Settled&&b.Day<=S.Clock.Today.TotalDays&&(b.Kind??"").Contains("rent"))
                        || (S.PendingEvent?.Title??"").Contains("租"));
                    WbAction(person,"打电话",()=>OpenPersonDialogue(id,remote:true),"WorkbenchKnownContact_"+id,96,urgent,36);if(urgent)highlighted=true;
                }
                WbHeading(list,"员工",18);
                foreach(var worker in S.Employees.Where(e=>string.IsNullOrEmpty(_wbContactSearch)||e.Person.Name.IndexOf(_wbContactSearch,StringComparison.OrdinalIgnoreCase)>=0))
                {
                    var employee=worker;string id=S.PersonIdOf(worker.Person);
                    var person=PersonSummary(list,id,employee.Person.Name,GameSession.RoleName(employee.Role)+" · "+S.EmployeeStatus(employee),64,ink:WbInk,muted:WbDim);
                    person.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight=false;
                    Ui.Size(Ui.Button(person,"打电话",()=>OpenPersonDialogue(id,remote:true),16,Ui.ButtonSecondary,S.RemoteConversationBlocker(id)==null),96,36).name="WorkbenchContact_"+employee.Person.Id;
                }
                if(!S.Employees.Any())WbText(list,"尚无员工",16,WbDim);
            }
            input.onValueChanged.AddListener(value=>{_wbContactSearch=value;Populate();});Populate();
            var schedule=WbColumn(row,"WorkbenchContactCalendar",1);WbHeading(schedule,"日程与邀约");
            var filter=Ui.Row(schedule,34,6);WbSegment(filter,new[]{"待回复","已安排"},_wbContactsFilter,n=>{_wbContactsFilter=n;_homeWorkbench?.RequestRebuild();},"WorkbenchScheduleFilter_",120,32);
            BuildWorkbenchActivityRows(WbScroll(schedule,"WorkbenchActivityList"),_wbContactsFilter==0,false);
            WbLink(schedule,"打开完整日程",OpenSchedulePanel,"WorkbenchCalendar",36);
        }

        private void BuildWorkbenchActivityRows(Transform parent,bool offered,bool compact)
        {
            var activities=S.CityActivities.Where(a=>compact ? a.State==CityActivityPhase.Offered||a.State==CityActivityPhase.Scheduled||a.State==CityActivityPhase.Gathering
                : offered ? a.State==CityActivityPhase.Offered : a.State==CityActivityPhase.Scheduled||a.State==CityActivityPhase.Gathering).OrderBy(a=>a.ScheduledStart).ToArray();
            int count=0;
            foreach(var activity in activities)
            {
                if(compact&&count>=2)break;
                string id=activity.Id;string title=CityLifeCatalog.Activities.FirstOrDefault(d=>d.Id==activity.DefinitionId)?.Name??activity.DefinitionId;
                string date=activity.ScheduledStart>0?new GameTimestamp(activity.ScheduledStart).ToString():"时间待定";
                if(compact)WbText(parent,title+" · "+date,14);
                else
                {
                    var card=Ui.FlatPanel(parent,"WorkbenchActivity_"+id,Ui.ButtonBg).rectTransform;Ui.VList(card,8,12);Ui.Size(card,-1,154);
                    WbHeading(card,title,18);WbText(card,date+" · "+(activity.State==CityActivityPhase.Offered?"待回复":"已安排"),15);
                    WbAction(card,"查看",()=>OpenCityActivityDetail(id),"WorkbenchInvitation_"+id,100,false,34);
                }
                count++;
            }
            if(count==0)WbText(parent,compact?"近期暂无日程":offered?"暂无待回复邀约":"暂无已安排日程",16,WbDim);
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class WorkbenchCashGraphic : MaskableGraphic
    {
        public double[] Income=Array.Empty<double>(),Expense=Array.Empty<double>();
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();if(Income.Length==0)return;var r=rectTransform.rect;
            double max=Math.Max(1,Math.Max(Income.Max(),Expense.Max()));float step=r.width/Income.Length;
            void Bar(float x,float width,float height,Color ink){int n=mesh.currentVertCount;foreach(var p in new[]{new Vector2(x,r.yMin),new Vector2(x,r.yMin+height),new Vector2(x+width,r.yMin+height),new Vector2(x+width,r.yMin)}){var v=UIVertex.simpleVert;v.position=p;v.color=ink;mesh.AddVert(v);}mesh.AddTriangle(n,n+1,n+2);mesh.AddTriangle(n,n+2,n+3);}
            for(int i=0;i<Income.Length;i++){float x=r.xMin+i*step+step*.1f;Bar(x,step*.32f,(float)(Income[i]/max)*(r.height-10),Ui.Good);Bar(x+step*.37f,step*.32f,(float)(Expense[i]/max)*(r.height-10),Ui.Warn);}
        }
    }
}
