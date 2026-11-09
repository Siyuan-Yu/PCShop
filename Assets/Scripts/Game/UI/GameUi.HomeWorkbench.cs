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
        // 与游戏其他窗口同一套深蓝金配色：窗口底色用 Ui.PanelBg，卡片用 Ui.PanelBg2。
        private static readonly Color WbCard=Ui.PanelBg2,WbInk=Ui.TextColor,WbDim=Ui.Dim;
        private Image _wbDayFill;
        private RectTransform _wbDayMarker;
        private Text _wbClockText;
        private static Text WbText(Transform parent,string text,int size=18,Color? color=null)
        {var t=Ui.Label(parent,text,size,TextAnchor.MiddleLeft,color??WbInk);Ui.Size(t,-1,size*1.6f);return t;}
        private static Button WbButton(Transform parent,string label,Action action,string name=null,bool selected=false,bool enabled=true)
        {
            var b=Ui.Size(Ui.Button(parent,label,action,17,selected?Ui.ButtonHot:Ui.ButtonBg,enabled),-1,40,1);
            if(name!=null)b.name=name;return b;
        }
        private static RectTransform WbScroll(Transform parent,string name)
        {
            var list=Ui.Scroll(parent);var area=(RectTransform)list.parent.parent;area.name=name;
            var layout=Ui.Size(area).GetComponent<LayoutElement>();layout.minHeight=80;layout.flexibleHeight=1;
            var areaImage=area.GetComponent<Image>();areaImage.color=Ui.FlatBand;Ui.FlatBorder(areaImage,Ui.FlatCardEdge);return list;
        }
        private static RectTransform WbColumn(Transform parent,string name,float weight=1)
        {
            var panel=Ui.FlatPanel(parent,name).rectTransform;Ui.Size(panel,-1,-1,weight);Ui.VList(panel,5,12);return panel;
        }
        private static void WbVital(Transform parent,string label,double value,double max)
        {
            WbText(parent,label+" "+value.ToString("0")+" / "+max.ToString("0"),17);
            var track=Ui.Panel(parent,"Vital_"+label,new Color32(21,28,38,255)).rectTransform;Ui.Size(track,-1,8);
            Ui.Panel(track,"Value",Ui.Accent).rectTransform.Anchor(0,0,(float)Math.Max(0,Math.Min(1,value/Math.Max(1,max))),1);
        }
        private static RectTransform WbPriceGraph(Transform parent,WorkbenchPriceDay[] data,float height)
        {
            var rect=Ui.Rect(parent,"WorkbenchPriceTrend");Ui.Size(rect,-1,height);
            rect.gameObject.AddComponent<CanvasRenderer>();
            var graphic=rect.gameObject.AddComponent<WorkbenchHistoryGraphic>();graphic.raycastTarget=false;graphic.Values=data.Select(d=>d.Price).ToArray();
            graphic.color=data.Length>1&&data.Last().Price<data.First().Price?new Color(.3f,.5f,.65f):Ui.Warn;return rect;
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
            UpdateWorkbenchDayBar();
        }
        public void OpenHomeWorkbench()
        {
            if(!S.CanUseHomeWorkbench){Toast("在家具城购买家庭经营大屏，送达后可在家打开工作台。");return;}
            if(_homeWorkbench!=null)return;
            _homeWorkbench=OpenWindow("居家工作台",1780,1000,BuildHomeWorkbench,holds:false,onClosed:()=>_homeWorkbench=null,scrollContent:false);
            // 大屏接近全屏：背后的 HUD 几乎压黑，看起来是一块独立的屏幕。
            var backdrop=_homeWorkbench.Root.GetComponent<Image>();if(backdrop!=null)backdrop.color=new Color(.02f,.03f,.05f,.94f);
            _homeWorkbench.LiveConversation=true;
            _homeWorkbench.LiveKey=()=>S.CanUseHomeWorkbench?(S.Clock.Now.TotalMinutes/30)+":"+S.StockNextQuote+":"+S.Ledger.Revision+":"+S.WorkbenchAlerts.Count+":"+S.Player.Health+":"+S.Player.Hunger:"closed";
            _homeWorkbench.LiveStamp=_homeWorkbench.LiveKey();
        }
        private void BuildHomeWorkbench(Window w)
        {
            if(!S.CanUseHomeWorkbench){w.Close();return;}
            var padding=w.Content.GetComponent<VerticalLayoutGroup>();padding.padding=new RectOffset(14,14,8,12);padding.spacing=6;
            var title=Ui.Row(w.Content,28,10);title.name="WorkbenchStatusBar";
            _wbClockText=WbText(title,S.Clock.Now+" · 在家",16);Ui.Size(_wbClockText,-1,28,1);
            Ui.Size(WbText(title,"现金 "+S.Ledger.Balance,18,Ui.Accent),230,28);
            Ui.Size(WbText(title,"净资产 "+S.NetWorth(),16),240,28);
            Ui.Size(WbText(title,"压力 "+S.Player.Stress.ToString("0")+"/100",16,S.Player.Stress>=70?Ui.Warn:WbDim),130,28);
            BuildWorkbenchDayBar(w.Content);
            // 一级页签 + 时间控制挤在同一行：页签靠左，暂停/倍速/托管靠右。
            var nav=Ui.Row(w.Content,38,6);nav.name="WorkbenchNavigation";
            string[] tabs={"总览","经营","投资","联系人"};
            for(int i=0;i<tabs.Length;i++){int index=i;WbMainTab(nav,tabs[i],()=>{_workbenchTab=index;w.RequestRebuild();},"WorkbenchTab_"+i,_workbenchTab==i);}
            WbFlex(nav);
            Ui.Size(WbControl(nav,S.Time.IsRunning?"暂停":"继续",()=>{S.Time.TogglePause();w.RequestRebuild();},"WorkbenchPause",false),76,34,0);
            foreach(int speed in TimeController.Speeds)
            {
                int chosen=speed;
                Ui.Size(WbControl(nav,TimeController.SpeedLabel(speed),()=>{S.Time.SetSpeed(chosen);w.RequestRebuild();},"WorkbenchSpeed_"+speed,S.Time.IsRunning&&S.Time.Speed==speed),50,34,0);
            }
            Ui.Size(WbControl(nav,"生活托管",OpenHomeAutomation,"WorkbenchAutomation",false),110,34,0);
            var body=Ui.Rect(w.Content,"WorkbenchBody");var le=Ui.Size(body).GetComponent<LayoutElement>();le.minHeight=160;le.flexibleHeight=1;Ui.VList(body,8,0);
            switch(_workbenchTab)
            {
                case 0:BuildWorkbenchPersonal(body,w);break;
                case 1:BuildWorkbenchBusiness(body,w);break;
                case 2:BuildWorkbenchInvestments(body,w);break;
                case 3:BuildWorkbenchContacts(body);break;
            }
        }
        /// <summary>一级页签：选中是金色实心，未选中是压暗的底色，一眼能看出当前在哪。</summary>
        private static Button WbMainTab(Transform parent,string label,Action action,string name,bool selected)
        {
            var b=Ui.Button(parent,label,action,18,selected?Ui.ButtonHot:Ui.FlatBand);
            var text=b.GetComponentInChildren<Text>();if(text!=null){text.color=selected?Ui.TextColor:WbDim;text.fontStyle=selected?FontStyle.Bold:FontStyle.Normal;}
            b.name=name;Ui.Size(b,136,38,0);return b;
        }
        /// <summary>二级页签 / 周期切换：选中只是浅蓝底加金字，比一级页签轻。</summary>
        private static Button WbSubTab(Transform parent,string label,Action action,string name,bool selected,float width=120,float height=32)
        {
            var b=Ui.Button(parent,label,action,16,selected?Ui.FlatRow:Ui.FlatBand);
            var text=b.GetComponentInChildren<Text>();if(text!=null)text.color=selected?Ui.Accent:WbDim;
            b.name=name;Ui.Size(b,width,height,0);return b;
        }
        private static Button WbControl(Transform parent,string label,Action action,string name,bool selected)
        {
            var b=Ui.Button(parent,label,action,16,selected?Ui.ButtonHot:Ui.ButtonBg);b.name=name;return b;
        }
        /// <summary>今日进度条：金色为已过去的时间，白线为现在；夜间（0–6 点、22–24 点）底色更暗。每帧由 UpdateWorkbenchDayBar 推进，不触发重建。</summary>
        private void BuildWorkbenchDayBar(Transform parent)
        {
            var row=Ui.Row(parent,10,0);row.name="WorkbenchDayBar";
            var holder=Ui.Rect(row,"DayTrack");Ui.Size(holder,-1,10,1);
            var back=Ui.Panel(holder,"DayTrackBack",new Color32(58,74,99,255));back.raycastTarget=false;back.rectTransform.Anchor(0,.5f,1,.5f,0,-4,0,-4);
            foreach(var night in new[]{(0f,.25f),(22f/24f,1f)})
            {var shade=Ui.Panel(back.transform,"Night",new Color32(24,30,42,255));shade.raycastTarget=false;shade.rectTransform.Anchor(night.Item1,0,night.Item2,1);}
            _wbDayFill=Ui.Panel(back.transform,"DayFill",Ui.ButtonHot);_wbDayFill.raycastTarget=false;_wbDayFill.rectTransform.Anchor(0,0,0,1);
            foreach(float tick in new[]{.25f,.5f,.75f}){var line=Ui.Panel(back.transform,"Tick",new Color(0,0,0,.35f));line.raycastTarget=false;line.rectTransform.Anchor(tick,0,tick,1,-.5f,0,-.5f,0);}
            var marker=Ui.Panel(holder,"DayNow",Color.white);marker.raycastTarget=false;_wbDayMarker=marker.rectTransform;_wbDayMarker.Anchor(0,0,0,1,-1.5f,0,-1.5f,0);
            UpdateWorkbenchDayBar();
        }
        private void UpdateWorkbenchDayBar()
        {
            if(_homeWorkbench==null||_homeWorkbench.Closed)return;
            if(_wbClockText!=null)_wbClockText.text=S.Clock.Now+" · 在家";
            if(_wbDayFill==null||_wbDayMarker==null)return;
            float progress=Mathf.Clamp01(S.Clock.MinuteOfDay/1440f);
            var fill=_wbDayFill.rectTransform;fill.anchorMax=new Vector2(progress,1);
            _wbDayMarker.anchorMin=new Vector2(progress,0);_wbDayMarker.anchorMax=new Vector2(progress,1);
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
            // 与手机证券是同一套看盘交易界面；这里不暂停时间，也不需要返回手机。
            BuildEmbeddedStock(w,parent);
        }
        private void BuildWorkbenchContacts(Transform parent)
        {
            // 只有「通讯录」和「日程」：通讯录就是下面这份名单，点头像看资料/邀约，右侧固定大小的按钮直接交谈。
            var actions=Ui.Row(parent,32,6);WbSubTab(actions,"通讯录",()=>{},"WorkbenchContacts",true,128);WbSubTab(actions,"日程",OpenCityActivities,"WorkbenchContactCalendar",false,128);WbFlex(actions);
            var list=WbScroll(parent,"WorkbenchContactsBody");
            foreach(var contact in S.SavedConversationContacts())
            {
                string id=contact.Id;
                var row=PersonSummary(list,id,contact.Name,contact.Role,54,()=>OpenCityPersonProfile(id),ink:WbInk,muted:WbDim);
                Ui.Size(Ui.Label(row,Npcs.LevelName(S.Relation(id))+" "+S.Relation(id),16,TextAnchor.MiddleCenter,Ui.Good),110);
                var talk=Ui.Size(Ui.Button(row,"交谈",()=>OpenPersonDialogue(id,remote:true),17,Ui.ButtonHot),100,44,0);talk.name="WorkbenchKnownContact_"+id;
            }
            foreach(var worker in S.Employees)
            {
                string personId=S.PersonIdOf(worker.Person);var employee=worker;
                var row=PersonSummary(list,personId,employee.Person.Name,GameSession.RoleName(employee.Role)+" · "+S.EmployeeStatus(employee),54,ink:WbInk,muted:WbDim);
                Ui.Size(Ui.Label(row,"员工",16,TextAnchor.MiddleCenter,WbDim),110);
                var talk=Ui.Size(Ui.Button(row,"交谈",()=>OpenPersonDialogue(personId,remote:true),17,Ui.ButtonHot,S.RemoteConversationBlocker(personId)==null),100,44,0);talk.name="WorkbenchContact_"+employee.Person.Id;
            }
        }        private void OpenHomeAutomation()
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
