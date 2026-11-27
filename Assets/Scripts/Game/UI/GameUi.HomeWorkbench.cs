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
        // 纸面内容与暖炭导航沿用 §18 定稿。
        private static readonly Color WbCard=Ui.PanelBg2,WbInk=Ui.TextColor,WbDim=Ui.Dim;
        private Image _wbDayFill;
        private RectTransform _wbDayMarker;
        private Text _wbClockText;
        private static Text WbText(Transform parent,string text,int size=18,Color? color=null)
        {var t=Ui.Label(parent,text,size,TextAnchor.MiddleLeft,color??WbInk);Ui.Size(t,-1,size*1.6f);return t;}
        private static Button WbButton(Transform parent,string label,Action action,string name=null,bool selected=false,bool enabled=true)
        {
            var b=Ui.Size(Ui.Button(parent,label,action,17,selected ? Ui.Chrome : Ui.ButtonBg,enabled),-1,40,1);
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
            var panel=Ui.FlatPanel(parent,name,Ui.ButtonBg).rectTransform;
            var size=Ui.Size(panel,0,-1,weight).GetComponent<LayoutElement>();size.minWidth=0;size.minHeight=0;
            Ui.VList(panel,7,14);return panel;
        }
        private static void WbVital(Transform parent,string label,double value,double max)
        {
            WbText(parent,label+" "+value.ToString("0")+" / "+max.ToString("0"),17);
            var track=Ui.Panel(parent,"Vital_"+label,Ui.Edge).rectTransform;Ui.Size(track,-1,8);
            Ui.Panel(track,"Value",Ui.Accent).rectTransform.Anchor(0,0,(float)Math.Max(0,Math.Min(1,value/Math.Max(1,max))),1);
        }
        private static RectTransform WbPriceGraph(Transform parent,WorkbenchPriceDay[] data,float height)
        {
            var rect=Ui.Rect(parent,"WorkbenchPriceTrend");Ui.Size(rect,-1,height);
            rect.gameObject.AddComponent<CanvasRenderer>();
            var graphic=rect.gameObject.AddComponent<WorkbenchHistoryGraphic>();graphic.raycastTarget=false;graphic.Values=data.Select(d=>d.Price).ToArray();
            graphic.color = data.Length > 1 ? Ui.ChangeColor(data.Last().Price - data.First().Price) : Ui.Dim;
            return rect;
        }
        private bool CanStartWorkbenchSetup => HomeDockAvailable && !_ctrl.Placing && S.Trip == null && !S.CityActivityExclusive
            && !S.DebugHomeWorkbenchEnabled && !S.HomeWorkbenchInstalled && S.OwnsHomeWorkbench;

        private void RefreshWorkbenchAccess()
        {
            if(_workbenchLauncher==null)
            {
                _workbenchLauncher=Ui.Button(_dockCanvas.transform,"居家工作台",OpenHomeWorkbench,17,Ui.ButtonHot);
                _workbenchLauncher.name="HomeWorkbenchLauncher";
                ((RectTransform)_workbenchLauncher.transform).Anchor(1,1,1,1,-232,-108,18,68);
                _workbenchLauncher.Tip(() => WorkbenchAccessReason);
            }
            var launcherRect = (RectTransform)_workbenchLauncher.transform;
            bool besideHomeToggle = _homeDockToggle.gameObject.activeSelf;
            float launcherLeft = besideHomeToggle ? -427 : -232;
            float launcherTop = (besideHomeToggle ? 74 : 68) + HudFunctionOffset;
            if (launcherRect.offsetMin.x != launcherLeft || launcherRect.offsetMax.y != -launcherTop)
                launcherRect.Anchor(1, 1, 1, 1, launcherLeft, -112 - HudFunctionOffset, besideHomeToggle ? 208 : 18, launcherTop);
            bool visible=S.CanDecorateCurrentResidence&&!_ctrl.Placing&&!S.IsGameOver&&!MainMenuOpen;
            _workbenchLauncher.gameObject.SetActive(visible);
            _workbenchLauncher.interactable=S.CanUseHomeWorkbench || CanStartWorkbenchSetup;
            _workbenchLauncher.GetComponentInChildren<Text>().text = S.DebugHomeWorkbenchEnabled ? "居家工作台 · 测试"
                : S.HomeWorkbenchInstalled ? "居家工作台" : S.OwnsHomeWorkbench ? "居家工作台 · 待安装"
                : S.HomeWorkbenchInTransit ? "居家工作台 · 配送中" : "居家工作台 · 米家有售";
            if(_homeWorkbench!=null&&!S.CanUseHomeWorkbench)_homeWorkbench.Close();
            UpdateWorkbenchDayBar();
        }
        public void OpenHomeWorkbench()
        {
            if (!S.CanUseHomeWorkbench)
            {
                if (CanStartWorkbenchSetup && S.FurnitureToPlace(false, GameSession.HomeWorkbenchId) > 0
                    && S.FurnitureEditBlocker(false) == null)
                    StartPlacement(false, GameSession.HomeWorkbenchId);
                else Toast(WorkbenchAccessReason);
                return;
            }
            if(_homeWorkbench!=null)return;
            _homeWorkbench=OpenWindow("居家工作台",1780,1000,BuildHomeWorkbench,holds:false,onClosed:()=>_homeWorkbench=null,scrollContent:false,fullScreen:true);
            // 全屏覆盖 HUD，内容区按当前画布尺寸伸展。
            var backdrop=_homeWorkbench.Root.GetComponent<Image>();if(backdrop!=null)backdrop.color=Ui.ChromeWell;
            _homeWorkbench.LiveConversation=true;
            _homeWorkbench.LiveKey = WorkbenchViewKey;
            _homeWorkbench.LiveStamp=_homeWorkbench.LiveKey();
        }

        private string WorkbenchViewKey()
        {
            if (!S.CanUseHomeWorkbench) return "closed";
            string key = $"{S.Clock.Now.TotalMinutes / 30}:{S.Time.IsRunning}:{S.Time.Speed}:{S.StockNextQuote}:{S.Ledger.Revision}:{S.WorkbenchAlerts.Count}:{S.ShopPurchaseReceiptRevision}";
            return _workbenchTab == 0 ? $"{key}:{S.Player.Health:0}:{S.Player.Hunger:0}" : key;
        }

        private Text _wbDayTime;
        private RectTransform _wbPage;
        private bool _wbTodoPrimary;
        private int _wbContactsFilter;
        private string _wbContactSearch = "";
        private int _wbRenderedTab = -1;
        private string _wbRenderedShop;

        private void BuildHomeWorkbench(Window w)
        {
            if (!S.CanUseHomeWorkbench) { w.Close(); return; }
            var padding = w.Content.GetComponent<VerticalLayoutGroup>() ?? Ui.VList(w.Content);
            var paper = w.Content.GetComponent<Image>() ?? w.Content.gameObject.AddComponent<Image>();
            paper.color = Ui.PanelBg; paper.raycastTarget = false;
            padding.padding = new RectOffset(16,16,10,14); padding.spacing = 12;
            var title = Ui.Row(w.Content, 40, 12); title.name = "WorkbenchStatusBar";
            _wbClockText = Ui.Size(WbText(title, S.Clock.Now + " · 在家", 16), 0, 36, 1.2f);
            var cash = Ui.Size(WbText(title, "", 22, Ui.Accent), 0, 36, 1);
            var worth = Ui.Size(WbText(title, "", 17), 0, 36, 1);
            var stress = Ui.Size(WbText(title, "", 16), 125, 36);
            var pause = Ui.Size(WbControl(title, "继续", () => { S.Time.TogglePause(); w.RefreshContent?.Invoke(); }, "WorkbenchPause", false), 80, 34);
            var speeds = new System.Collections.Generic.List<(Button button, int speed)>();
            foreach (int speed in TimeController.Speeds)
            {
                int chosen = speed;
                var button = Ui.Size(WbControl(title, TimeController.SpeedLabel(speed), () => { S.Time.SetSpeed(chosen); w.RefreshContent?.Invoke(); }, "WorkbenchSpeed_" + speed, false), 54, 34);
                speeds.Add((button, chosen));
            }
            w.RefreshContent = () =>
            {
                cash.text = "现金 " + S.Ledger.Balance; worth.text = "净资产 " + S.NetWorth();
                stress.text = $"压力 {S.Player.Stress:0}/100"; stress.color = S.Player.Stress >= 70 ? Ui.Warn : WbDim;
                pause.GetComponentInChildren<Text>().text = S.Time.IsRunning ? "暂停" : "继续";
                foreach (var pair in speeds) pair.button.GetComponentInChildren<Text>().fontStyle = S.Time.Speed == pair.speed ? FontStyle.Bold : FontStyle.Normal;
            };
            BuildWorkbenchDayBar(w.Content);
            _wbPage = Ui.Rect(w.Content, "WorkbenchWorkspace"); Ui.VList(_wbPage, 0, 0);
            Ui.Size(_wbPage).GetComponent<LayoutElement>().flexibleHeight = 1;
            w.RebuildContent = () => RefreshWorkbenchPage(w);
            RefreshWorkbenchPage(w); w.RefreshContent();
        }

        private void RefreshWorkbenchPage(Window w)
        {
            if (!S.CanUseHomeWorkbench) { w.Close(); return; }
            var scrolled = _wbRenderedTab == _workbenchTab && _wbRenderedShop == _workbenchShop && !w.ResetScrollOnRebuild ? Ui.SaveScroll(_wbPage) : null;
            w.ResetScrollOnRebuild = false;
            Ui.Clear(_wbPage);
            var workspace = Ui.Row(_wbPage, -1, 16); Ui.Size(workspace).GetComponent<LayoutElement>().flexibleHeight = 1;
            var nav = Ui.FlatPanel(workspace, "WorkbenchNavigation", Ui.FlatBand).rectTransform;
            Ui.Size(nav, 184, -1, 0); Ui.VList(nav, 10, 8);
            string[] tabs = { "今日", "门店", "投资", "财务", "联系人" };
            int todos = WorkbenchTodoCount();
            int shops = S.MyShops.Count(shop => S.PlayerManagesShop(shop)&&!S.IsOwnedVenue(shop.Place) && (WbStockCount(shop) == 0 || !S.Employees.Any(e => e.Shop == shop)));
            for (int i = 0; i < tabs.Length; i++)
            {
                int index = i;
                var button = Ui.Size(Ui.Button(nav, tabs[i], () => { _workbenchTab = index; w.RequestRebuild(); }, 23, _workbenchTab == i ? Ui.Chrome : Ui.ButtonBg), -1, 70);
                button.name = "WorkbenchTab_" + i;
                int badge = i == 0 ? todos : i == 1 ? shops : 0;
                if (badge > 0)
                {
                    var bg = Ui.FlatPanel(button.transform, "Badge", Ui.Warn, Ui.Warn); bg.rectTransform.Anchor(1,1,1,1,-27,-26,4,3);
                    var label = Ui.Label(bg.transform, badge.ToString(), 14, TextAnchor.MiddleCenter, Color.white); label.rectTransform.Fill(); label.raycastTarget = false;
                }
            }
            var flex = Ui.Rect(nav, "NavSpace"); Ui.Size(flex).GetComponent<LayoutElement>().flexibleHeight = 1;
            Ui.Size(Ui.Button(nav, "生活托管", OpenHomeAutomation, 17, Ui.ButtonSecondary), -1, 44).name = "WorkbenchAutomation";
            var a = S.HomeAutomation; int enabled = new[] { a.Meals,a.Takeaway,a.Sleep,a.Clothing,a.Supplies }.Count(x => x);
            Ui.Size(Ui.Label(nav, "已开启 " + enabled + " 项", 14, TextAnchor.MiddleCenter, WbDim), -1, 24);
            var body = Ui.Rect(workspace, "WorkbenchBody"); Ui.Size(body, 0, -1, 1); Ui.VList(body, 12, 0);
            switch (_workbenchTab)
            {
                case 0: BuildWorkbenchPersonal(body,w); break;
                case 1: BuildWorkbenchBusiness(body,w); break;
                case 2: BuildWorkbenchInvestments(body,w); break;
                case 3: BuildWorkbenchFinance(body,w); break;
                case 4: BuildWorkbenchContacts(body); break;
            }
            w.RefreshContent?.Invoke();
            Ui.RestoreScroll(w.Root,_wbPage,scrolled);
            _wbRenderedTab = _workbenchTab; _wbRenderedShop = _workbenchShop;
            if (w.LiveKey != null) w.LiveStamp = w.LiveKey();
        }
        private static Button WbMainTab(Transform parent,string label,Action action,string name,bool selected)
        {
            var b=Ui.Button(parent,label,action,18,selected ? Ui.Chrome : Ui.ButtonBg);
            b.name=name;Ui.Size(b,136,38,0);return b;
        }
        private static Button WbSubTab(Transform parent,string label,Action action,string name,bool selected,float width=120,float height=32)
        {
            var b=Ui.Button(parent,label,action,16,selected ? Ui.Chrome : Ui.ButtonBg);
            b.name=name;Ui.Size(b,width,height,0);return b;
        }
        private static Button WbControl(Transform parent,string label,Action action,string name,bool selected)
        {
            var b=Ui.Button(parent,label,action,16,selected ? Ui.Chrome : Ui.ButtonBg);b.name=name;return b;
        }
        /// <summary>两色全高日进度带；外壳常驻，逐帧只推进填充、刻线和时间文字。</summary>
        private void BuildWorkbenchDayBar(Transform parent)
        {
            var track = Ui.FlatPanel(parent, "WorkbenchDayBar", new Color(.227f,.216f,.2f,.12f), new Color(.227f,.216f,.2f,.6f));
            Ui.Size(track,-1,88); track.raycastTarget = false;
            var clip = Ui.Panel(track.transform, "DayClip", Color.white); Ui.Round(clip); clip.rectTransform.Fill();
            clip.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            _wbDayFill = Ui.Panel(clip.transform,"DayFill",new Color32(196,132,34,150));
            _wbDayFill.raycastTarget = false; _wbDayFill.rectTransform.Anchor(0,0,0,1);
            for (int i = 1; i < 4; i++)
            {
                var line = Ui.Panel(track.transform,"Tick_"+i,new Color(.227f,.216f,.2f,.27f)); line.raycastTarget=false;
                line.rectTransform.Anchor(i/4f,0,i/4f,1,-.5f,0,-.5f,0);
            }
            string[] periods = { "凌晨","上午","下午","夜晚" };
            for (int i=0;i<4;i++)
            {
                var label=Bold(Ui.Label(track.transform,periods[i],13,TextAnchor.UpperCenter,Ui.Chrome));
                label.rectTransform.Anchor(i/4f,.65f,(i+1)/4f,1,0,0,0,5);label.raycastTarget=false;
            }
            for(int i=0;i<=4;i++)
            {
                var label=Bold(Ui.Label(track.transform,(i*6)+":00",12,i==0?TextAnchor.LowerLeft:i==4?TextAnchor.LowerRight:TextAnchor.LowerCenter,Ui.Chrome));
                label.rectTransform.Anchor(i/4f,0,i/4f,.3f,i==0?6:i==4?-76:-34,4,i==4?6:i==0?-76:-34,0);label.raycastTarget=false;
            }
            var marker = Ui.Panel(track.transform,"DayNow",Ui.Chrome); marker.raycastTarget=false;
            _wbDayMarker=marker.rectTransform;_wbDayMarker.Anchor(0,0,0,1,-1.5f,0,-1.5f,0);
            var pill=Ui.FlatPanel(_wbDayMarker,"CurrentTime",Ui.Chrome,Ui.Chrome);pill.rectTransform.Anchor(.5f,.5f,.5f,.5f,-40,-16,-40,-16);
            _wbDayTime=Bold(Ui.Label(pill.transform,"",16,TextAnchor.MiddleCenter,Color.white));_wbDayTime.rectTransform.Fill();_wbDayTime.raycastTarget=false;
            UpdateWorkbenchDayBar();
        }
        private void UpdateWorkbenchDayBar()
        {
            if(_homeWorkbench==null||_homeWorkbench.Closed)return;
            if(_wbClockText!=null)_wbClockText.text=S.Clock.Now+" · 在家";
            if(_wbDayFill==null||_wbDayMarker==null)return;
            float progress=Mathf.Clamp01(S.Clock.MinuteOfDay/1440f);
            _wbDayFill.rectTransform.anchorMax=new Vector2(progress,1);
            _wbDayMarker.anchorMin=new Vector2(progress,0);_wbDayMarker.anchorMax=new Vector2(progress,1);
            if(_wbDayTime!=null)_wbDayTime.text=GameSession.ClockText(S.Clock.MinuteOfDay);
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
                var sim=shop.Sim;Ui.Size(ShelfSaleTag(w,w.Content,"goods_"+id,sim.GoodsPriceOf(id).Yuan,sim.GoodsPriceRange(id),v=>sim.GoodsPurchaseEstimateAtPrice(id,v),
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
                    Line(w.Content, $"{PartCatalog.TierName(order.Tier)}整机 · {order.Price} · {S.Clock.OrderDeadlineText(order.DueAt.TotalMinutes)}", 20);
                    Line(w.Content, order.EmployeeMinutesLeft > 0 ? $"员工装机剩余 {order.EmployeeMinutesLeft} 分钟" :
                        order.PartsReserved ? "配件已备齐 · 等待装机" : "等待配件", 17);
                    foreach (var part in PartCatalog.BuildList(order.Tier))
                        Line(w.Content, part.Name + " · " + (order.HasReservedPart(part) ? "已预留" : "缺货"), 16, Ui.Dim);
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
                {Ui.Size(Ui.Button(w.Content,title+" · "+(enabled?"开 ✓":"关"),()=>Apply(edit),18,Ui.SelectedButton(enabled)),-1,40).name="Automation_"+key;}
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
                Para(w.Content,"办公台安装后，生活托管会在你入住的住宅运行，使用当前住处的设施和后仓。缺货、缺设施或预算不足时会提醒。",16,Ui.Dim);
            });
        }
    }
}
