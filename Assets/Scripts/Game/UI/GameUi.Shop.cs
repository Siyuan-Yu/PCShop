using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Session;
using PCShopSell.Core.Shop;
using PCShopSell.Core.Time;
using PCShopSell.Core.Trade;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private enum ShopTab
        {
            Counter,
            Stock,
            Pricing,
            Orders,
            Renovate,
            Staff,
        }

        private static readonly (ShopTab tab, string name)[] Tabs =
        {
            (ShopTab.Counter, "柜台"), (ShopTab.Stock, "库存"), (ShopTab.Pricing, "定价"),
            (ShopTab.Orders, "订单"), (ShopTab.Renovate, "装修"), (ShopTab.Staff, "员工"),
        };

        private RectTransform _dock;
        private RectTransform _dockContent;
        private readonly List<(Button b, ShopTab tab)> _tabButtons = new List<(Button, ShopTab)>();
        private ShopTab _tab = ShopTab.Counter;
        private Button _shopDockToggle;
        private bool _dockCollapsed;
        private string _dockShopId;

        private void BuildShopDockToggle(Transform parent)
        {
            _shopDockToggle=Ui.Button(parent,"收起店铺面板 ▴",ToggleShopDock,17);
            _shopDockToggle.name="ShopDockToggle";
            ((RectTransform)_shopDockToggle.transform).Anchor(1,1,1,1,-200,-112,12,74);
            _shopDockToggle.gameObject.SetActive(false);
        }
        private void ToggleShopDock()
        {
            if(!S.InPlayerShop||S.IsOwnedVenue(S.CurrentPlace)||_ctrl.Placing)return;
            _dockCollapsed=DockOpen;
            if(_dockCollapsed)CloseDock();else OpenDock();
            RefreshShopDockVisibility();
        }
        private void RefreshShopDockVisibility()
        {
            bool inside=S.InPlayerShop&&!S.IsOwnedVenue(S.CurrentPlace);
            bool visible=inside&&!_ctrl.Placing&&!S.IsGameOver&&!MainMenuOpen;
            if(_shopDockToggle!=null)_shopDockToggle.gameObject.SetActive(visible);
            if(!inside){CloseDock();_dockShopId=null;_dockCollapsed=false;return;}
            if(_dockShopId!=S.CurrentShop.Place.Id){_dockShopId=S.CurrentShop.Place.Id;_dockCollapsed=false;}
            if(!visible){CloseDock();return;}
            if(!_dockCollapsed&&!DockOpen)OpenDock();
            if(_shopDockToggle!=null)_shopDockToggle.GetComponentInChildren<Text>().text=DockOpen?"收起店铺面板 ▴":"展开店铺面板 ▾";
        }

        public bool DockOpen => _dock != null;

        public void OpenDock()
        {
            if(S.IsOwnedVenue(S.CurrentPlace)){OpenVenueManagement(S.CurrentPlace);return;}
            if(!S.InPlayerShop)return;
            _dockCollapsed=false;_dockShopId=S.CurrentShop.Place.Id;
            if (_dock != null) return;
            Image panel = Ui.Panel(_dockCanvas.transform, "ShopDock", Ui.PanelBg);
            _dock = panel.rectTransform.Anchor(1, 0, 1, 1, -600, 70, 10, 120);

            RectTransform tabs = Ui.Rect(_dock, "Tabs").Anchor(0, 1, 1, 1, 8, -54, 8, 8);
            var h = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 4;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = true;
            _tabButtons.Clear();
            foreach (var (tab, name) in Tabs)
            {
                ShopTab t = tab;
                _tabButtons.Add((Ui.Button(tabs, name, () => SelectTab(t), 18), t));
            }

            _dockContent = Ui.Scroll(_dock);
            ((RectTransform)_dockContent.parent.parent).Anchor(0, 0, 1, 1, 0, 60, 0, 58);

            bool staffed = S.IsEmployeeOnDuty(S.Clock.Now);
            Button leave = Ui.Button(_dock, "离开店铺", ConfirmLeaveShop, 19, Ui.Warn);
            ((RectTransform)leave.transform).Anchor(0, 0, 1, 0, 12, 8, 12, -52);

            SelectTab(_tab);
        }

        private Window _leaveConfirm;

        /// <summary>离开店铺前的二次确认：店里没人就关门，店员在班就照常营业。</summary>
        public void ConfirmLeaveShop()
        {
            if (_leaveConfirm != null) return;
            if (S.LeaveBlocker != null)
            {
                Toast(S.LeaveBlocker);
                return;
            }
            bool staffed = S.IsEmployeeOnDuty(S.Clock.Now);
            if (staffed) { _ctrl.LeaveShop(); return; }
            int inShop = S.PlayerShop.Queue.Count;
            _leaveConfirm = OpenWindow("要离开店铺吗？", 620, 320, w =>
            {
                if (staffed)
                    Para(w.Content, $"店员{S.OnDutyStaffName}在班，你走了店<color=#7FD18B>照常营业</color>，顾客由他来接待、结账。", 19);
                else
                {
                    Para(w.Content, "店里现在只有你一个人，你一走店就<color=#D79780>关门</color>。", 19);
                    Para(w.Content, inShop > 0 ? $"柜台前还有{inShop}个顾客在排队，他们会白等一场走掉；关门期间也不会有人进店。" : "关门期间不会有顾客进店。", 17, Ui.Dim);
                    if (S.Employee != null) Para(w.Content, $"（店员{S.Employee.Name}现在不在班。）", 16, Ui.Dim);
                }
                RectTransform row = Ui.Row(w.Content, 50, 12);
                Ui.Size(Ui.Button(row, "再待会儿", w.Close, 19), -1, -1, 1);
                Ui.Size(Ui.Button(row, staffed ? "离开" : "关门离开", () =>
                {
                    w.Close();
                    _ctrl.LeaveShop();
                }, 19, Ui.Warn), -1, -1, 1);
            }, onClosed: () => _leaveConfirm = null);
        }

        public void CloseDock()
        {
            if (_dock == null) return;
            Kill.It(_dock.gameObject);
            _dock = null;
            _counterRefresh.Clear();
            _counterStructure = null;
        }

        /// <summary>自动化冒烟用：切换商店面板的标签页（按枚举序号）。</summary>
        public int DebugTabCount => Tabs.Length;

        public void DebugSelectTab(int index)
        {
            if (_dock != null&&index>=0&&index<Tabs.Length) SelectTab(Tabs[index].tab);
        }

        private void SelectTab(ShopTab tab)
        {
            _tab = tab;
            // 店铺侧栏不锁住时间；所有页签都服从顶部的暂停 / 继续与倍速。
            foreach (var (b, t) in _tabButtons) b.image.color = t == tab ? Ui.ButtonHot : Ui.ButtonBg;
            RebuildDock();
        }

        private void RefreshDock()
        {
            if (_dock == null) return;
            if (!S.InPlayerShop)
            {
                CloseDock();
                return;
            }
            // Keep the current controls alive while the player is choosing or scrolling a dropdown.
            if (_dock.GetComponentsInChildren<ChoiceDropdown>().Any(choice=>choice.IsChoiceOpen)||_dock.GetComponentsInChildren<InputField>().Any(input=>input.isFocused)) return;
            if((_tab==ShopTab.Stock || _tab==ShopTab.Pricing) && !AnyModalOpen && _stockViewSignature!=StockViewSignature())
            {RebuildDock();return;}
            if(_tab==ShopTab.Renovate&&!AnyModalOpen&&_renovationStockSignature!=RenovationStockSignature())
            {RebuildDock();return;}
            if (_tab == ShopTab.Counter || (_tab==ShopTab.Orders&&S.IsSpecialBusiness(S.CurrentShop)))
            {
                if (_counterStructure != CounterStructure()) RebuildDock();
                else foreach (var refresh in _counterRefresh) refresh();
            }
            else if (_tab == ShopTab.Staff &&
                (_staffRefreshMinute != S.Clock.Now.TotalMinutes || _staffCandidateCount != S.Candidates.Count || _staffRosterSignature!=StaffRosterSignature())) RebuildDock();
        }

        private string _counterStructure;
        private readonly List<System.Action> _counterRefresh = new List<System.Action>();
        private string CounterStructure()
        {
            var shop = S.PlayerShop;
            var quest = S.CurrentQuest();
            string guide = quest?.Target == TargetFor(_tab) ? quest.Id + ":" + quest.Progress?.Invoke(S) + ":" + quest.LessonReady?.Invoke(S) : quest?.Id;
            // Rows/buttons only rebuild on structural changes; ticking numbers keep their Text objects.
            return S.CurrentShop.Place.Id + "|" + S.RetailKind + "|" + S.Clock.Today.TotalDays + "|" + guide
                + "|" + S.ShopFitGap() + "|" + S.IsRenovating + "|" + S.CurrentShop.OwnsProperty
                + "|" + shop.OpenMinute + ":" + shop.CloseMinute + ":" + shop.InHours(S.Clock.Now) + ":" + shop.Accepted
                + "|" + S.Employee?.Id + ":" + S.Employee?.OnDuty(S.Clock.Now)
                + "|" + S.Salesperson?.Id + ":" + (S.Salesperson != null ? GameSession.EmployeeCharm(S.Salesperson) : 0) + ":" + shop.SalesPurchaseIntentBonus
                + "|" + shop.ClothingMirrorPurchaseIntentBonus + "|" + shop.Orders.Count + "|" + S.Online.Pending.Count
                + "|" + string.Join(",", shop.Queue.Select(c => c.Id))
                + (S.IsSpecialBusiness(S.CurrentShop) ? "|" + S.Clock.Now.TotalMinutes : "");
        }
        private Text CounterText(System.Func<string> value, int size, Color? color = null)
        {
            var text = Line(C, value(), size, color);
            _counterRefresh.Add(() => text.text = value());
            return text;
        }
        private string CounterQueueText(ShopCustomer customer)
        {
            long waited = customer.WaitingSince.MinutesUntil(S.Clock.Now);
            double? seconds = S.PlayerShop.CheckoutElapsedSeconds(customer);
            string patience = seconds.HasValue ? (seconds.Value >= 12 ? $"<color=#D79780>已等 {seconds.Value:0} 秒，请尽快结账</color>" : $"已等 {seconds.Value:0} 秒") : $"等了 {waited} 游戏分钟";
            return $"{customer.ItemName}（{(customer.DisplayTier >= 0 ? "展示机" : customer.GoodsId != null ? "杂货" : customer.ItemBoxed ? "盒装" : "散件")}） <color=#A9C9A9>{customer.Spend}</color>\n<size=14>{patience}</size>";
        }

        private ShopTab? _scrollTab;
        private string _stockViewSignature;
        private string StockViewSignature()=>S.CurrentShop.Place.Id+"|"+S.RetailKind+"|"
            +string.Join(";",S.PlayerShop.Stock.Lines().Select(l=>l.Part.Id+":"+l.Boxed+":"+l.Count))+"|"
            +string.Join(";",S.PlayerShop.Stock.GoodsLines().Select(l=>l.Key+":"+l.Value))+"|"+S.HasWarehouseHere+"|"
            +string.Join(";",S.ShopLayout.Select(p=>p.Id+":"+p.Item+":"+p.DisplayLimit))+"|"
            +string.Join(";",CollectBox().Select(b=>b.Key+":"+b.Count+":"+b.Kg))+"|"+string.Join(",",S.PlayerShop.DisplayPcs);

        public void RebuildDock()
        {
            if (_dock == null) return;
            var scrolled = _scrollTab == _tab ? Ui.SaveScroll(_dock) : null;
            _scrollTab = _tab;
            _counterRefresh.Clear();
            Ui.Clear(_dockContent);
            var guide = S.CurrentQuest();
            foreach (var (button, tab) in _tabButtons)
            {
                bool recommended = guide != null && guide.Target == TargetFor(tab);
                button.GetComponentInChildren<Text>().text = (recommended ? "› " : "") + Tabs.First(t => t.tab == tab).name;
            }
            GuideHint(_dockContent, TargetFor(_tab), RebuildDock);
            switch (_tab)
            {
                case ShopTab.Counter: BuildCounter(); break;
                case ShopTab.Stock: BuildStock(); break;
                case ShopTab.Pricing: BuildPricing(); break;
                case ShopTab.Orders: if(S.IsSpecialBusiness(S.CurrentShop))BuildSpecialCounter();else BuildOrders(); break;
                case ShopTab.Renovate: BuildRenovate(); break;
                case ShopTab.Staff: BuildStaff(); break;
            }
            Ui.RestoreScroll(_dock, _dock, scrolled);
            if (scrolled == null) _dockContent.anchoredPosition = Vector2.zero;
            if(_tab==ShopTab.Stock || _tab==ShopTab.Pricing)_stockViewSignature=StockViewSignature();
            if(_tab==ShopTab.Counter || (_tab==ShopTab.Orders&&S.IsSpecialBusiness(S.CurrentShop)))_counterStructure=CounterStructure();
        }

        private Transform C => _dockContent;

        // ---------- 柜台 ----------

        /// <summary>柜台内直接选择开门、关门时间；不限时单独切换，不再打开设置弹窗。</summary>
        private void ShopHoursRow(ShopSimulation shop)
        {
            const int day=24*60;
            bool allDay=shop.AllDay;
            int open=allDay?9*60:shop.OpenMinute;
            int close=allDay?21*60:shop.CloseMinute%day;
            RectTransform row=Ui.Row(C,36,6);
            Ui.Size(Ui.Label(row,"营业时间",16),76,36);
            void TimeChoice(bool opening)
            {
                int other=opening?close:open;
                var minutes=Enumerable.Range(0,24).Select(i=>i*60)
                    .Where(m=>allDay||m!=other).ToArray();
                string prefix=opening?"开门 ":"关门 ";
                var labels=minutes.Select(m=>prefix+Ui.Hm(m)).ToList();
                int selected=System.Array.IndexOf(minutes,opening?open:close);
                bool placeholder=allDay||selected<0;
                if(placeholder){labels.Insert(0,opening?"开门时间":"关门时间");selected=0;}
                var dropdown=Ui.Choice(row,labels,selected,index=>
                {
                    if(placeholder&&index==0)return;
                    int value=minutes[index-(placeholder?1:0)];
                    int start=opening?value:open,end=opening?close:value;
                    if(start==end){Toast("开门和关门不能相同；全天营业请选择“不限时”。");RebuildDock();return;}
                    shop.SetHours(start,end==0?day:end);
                    RebuildDock();
                });
                dropdown.name=opening?"ShopOpeningTime":"ShopClosingTime";
                Ui.Size(dropdown,145,36);
                dropdown.captionText.fontSize=15;
            }
            TimeChoice(true);TimeChoice(false);
            var unlimited=Ui.Button(row,allDay?"不限时 ✓":"不限时",()=>
            {
                if(shop.AllDay)shop.SetHours(9*60,21*60);
                else shop.SetHours(0,day);
                RebuildDock();
            },15,allDay?Ui.ButtonHot:Ui.ButtonBg);
            unlimited.name="ShopUnlimitedHours";Ui.Size(unlimited,90,36);
            if(!allDay&&close<open&&close!=0)Line(C,"跨午夜营业，次日 "+Ui.Hm(close)+" 关门。",14,Ui.Dim);
            if(!allDay&&!shop.InHours(S.Clock.Now))
                Line(C,$"现在不在营业时间，{Ui.Hm(shop.OpenMinute)} 才开门；人在店里也不接客。",15,new Color32(231,160,135,255));
        }

        private void BuildCounter()
        {
            BuildCompetitionEntry(C);
            if(S.IsSpecialBusiness(S.CurrentShop)){BuildSpecialCounter();return;}
            BuildIndustryBrief(C);
            ShopSimulation shop = S.PlayerShop;
            bool fitBanner = !S.ShopFitted || S.IsRenovating;
            FitBanner(C);
            var management=Ui.Row(C,42,6);
            Ui.Size(Ui.Button(management,"本店主售："+RetailShopTypes.Name(S.RetailKind)+" · 点击更改",OpenRetailTypeChooser,18,Ui.ButtonHot),-1,42,1).name="ShopMainCategory";
            var closeShop=Ui.Size(Ui.Button(management,S.CurrentShop.OwnsProperty?"结束自营":"退租店铺",()=>ConfirmCloseShop(S.CurrentShop),16,Ui.Warn),110,42);
            closeShop.name="CloseShopCounter";
            CounterText(() => $"今日进店 {shop.TodayCustomers} 人 · 流失 {shop.TodayLost} 人 · 营业额 {shop.TodaySales}", 18);
            CounterText(() => $"店里 {shop.ActiveCustomers.Count} 位顾客，{shop.Queue.Count} 位在排队结账（{PaymentText(shop.Accepted)}）", 17, Ui.Dim);
            ShopHoursRow(shop);
            if(S.Salesperson!=null)
                Line(C,$"销售员 {S.Salesperson.Name} · 魅力 {GameSession.EmployeeCharm(S.Salesperson)} · "+
                    (shop.SalesPurchaseIntentBonus>0?$"在岗：购买意愿 +{shop.SalesPurchaseIntentBonus*100:0.#}个百分点":"未在岗：暂无销售加成"),16,Ui.Good);
            if(shop.RetailKind=="clothing" && shop.ClothingMirrorPurchaseIntentBonus>0)
                Line(C,"全身镜：服装购买意愿 +5个百分点（多面不叠加）",16,Ui.Good);
            if (S.Employee != null)
                Line(C, $"店员 {S.Employee.Name}：{(S.Employee.OnDuty(S.Clock.Now) ? "在岗" : "下班")}（在班持续收银，你可以协助或装机）", 16, Ui.Dim);

            bool atCounter = _ctrl.Interior != null && _ctrl.Interior.NearCounter;
            if (shop.Queue.Count > 0)
                CounterText(() => _ctrl.Interior != null && _ctrl.Interior.NearCounter ? "你在收银台：点“结账”或按 F，把货交给顾客、收钱。" : "<color=#D79780>顾客在收银台前排队等你。走到收银台后面才能结账。</color>", 16);
            RectTransform list = Ui.Scroll(C, fitBanner ? 200 : 420);
            foreach (ShopCustomer c in shop.Queue)
            {
                ShopCustomer cust = c;
                RectTransform row = Ui.Row(list, 44);
                var description = Ui.Size(Ui.Label(row, CounterQueueText(cust), 17), 360);
                var checkout = Ui.Size(Ui.Button(row, "结账", () => _ctrl.Checkout(cust.Id), 18, Ui.ButtonHot, atCounter), 100);
                _counterRefresh.Add(() => { description.text = CounterQueueText(cust); checkout.interactable = _ctrl.Interior != null && _ctrl.Interior.NearCounter; });
            }
            if (shop.Queue.Count == 0) Ui.Size(Ui.Label(list, "没人排队。顾客挑好货会过来，现场等结账至少约 15 秒（暂停不计）。", 17, TextAnchor.MiddleLeft, Ui.Dim), -1, 30);

            int orders = shop.Orders.Count;
            if (orders > 0) Line(C, $"有 {orders} 个订单等你处理（去“订单”页）", 18, Ui.Warn);
            if (S.Online.Pending.Count > 0) Line(C, $"1788 有 {S.Online.Pending.Count} 单在路上，明天 10:00 送到", 17, Ui.Dim);
            CounterText(() => $"后仓容量不限 · 已存 {shop.Stock.UsedSpace:0.#} 格 · 全局加价 {shop.Markup:0.00}×", 17, Ui.Dim);

            Ui.Size(Ui.Button(C, "经营报表（近 14 天收入曲线）", OpenReportWindow, 18), -1, 40);
        }

        // ---------- 库存 ----------

        private void BuildStock()
        {
            BuildRetailLinks(C);
            ShopSimulation shop = S.PlayerShop;
            Line(C, "货架上的商品", 19, Ui.Accent).name="StockShelvesHeading";
            var shelfIdx = S.ShelfIndexes();
            if (shelfIdx.Count == 0) Line(C, "尚未摆放货架。", 16, Ui.Dim);
            for (int si = 0; si < shelfIdx.Count; si++)
            {
                int layoutIdx = shelfIdx[si];
                ShelfKind sk = S.ShelfKindAt(layoutIdx);
                FurnitureDef sdef = FurnitureCatalog.Get(S.ShopLayout[layoutIdx].Id);
                string tier = sdef != null && sdef.ShelfKg > ShelfKinds.CapacityKg ? (sdef.ShelfKg >= 160 ? "重型" : "加固") : "普通";
                RectTransform srow = Ui.Row(C, 38, 4);
                Ui.Size(Ui.Label(srow, $"<b>{si + 1}号</b> <size=12><color=#ABB5A8>{tier}</color></size>", 16, TextAnchor.MiddleLeft), 78);
                Ui.Size(Ui.Button(srow, sk == null ? "选择摆什么 ▾" : sk.Name + "  ▾", () => OpenShelfKindChooser(layoutIdx), 16, sk == null ? Ui.Warn : Ui.ButtonBg), 150);
                string load = $"0/{sdef?.ShelfKg ?? 0:0} 千克\n<size=12>0/{sdef?.ShelfItems ?? 0} 件</size>";
                if (sk != null)
                {
                    ShelfFill sf = shop.ShelfFillOf(sk);
                    load = $"{sf.UsedKg:0.##}/{sf.CapacityKg:0} 千克\n<size=12>{sf.Total}/{sf.CapacityItems} 件 · 同类合计</size>";
                }
                Ui.Size(Ui.Label(srow, load, 14, TextAnchor.MiddleLeft, Ui.Dim), 178).name = $"ShelfCapacity_{layoutIdx}";
                Ui.Size(Ui.Button(srow, "查看", () => OpenShelfWindow(layoutIdx), 15, sk == null ? Ui.ButtonBg : Ui.ButtonHot, sk != null), 66);
            }

            // Stock is shared with the shelf planner. Split its live result from the
            // remainder, instead of displaying all Stock as if everything were on sale.
            var shown=new Dictionary<string,int>();
            var shelves=Ui.Rect(C,"StockShelvesSection");Ui.VList(shelves,4,0);
            foreach(var kind in ShelfKinds.All)
            {
                var fill=shop.ShelfFillOf(kind);
                foreach(var line in fill.Parts)
                {
                    var part=line.Part;bool boxed=line.Boxed;
                    string key="part:"+part.Id+(boxed?":b":":l");shown[key]=line.Count;
                    var row=Ui.Row(shelves,60,4);row.name="ShelfPart_"+part.Id+(boxed?"_b":"_l");
                    Ui.Size(ProductText(row,ProductLabel(part.Name+(boxed?" · 盒装":" · 散件"),PartUnitKg(part,boxed)),16),-1,-1,1);
                    StockCount(row,line.Count);
                    if(boxed)
                    {
                        Ui.Size(Ui.Button(row,"拆 1",()=>{S.Unbox(part,1);RebuildDock();},16),64);
                        Ui.Size(Ui.Button(row,"全拆",()=>{S.Unbox(part,line.Count);RebuildDock();},16),64);
                    }
                }
                foreach(var line in fill.Goods)
                {
                    string id=line.Key;var product=Gifts.Get(id);shown["stockgift:"+id]=line.Value;
                    var row=Ui.Row(shelves,60,4);row.name="ShelfGoods_"+id;
                    Ui.Size(ProductText(row,ProductLabel(product.Name,product.WeightKg,kind.Name),16),-1,-1,1);
                    StockCount(row,line.Value);
                    if(S.HasWarehouseHere)Ui.Size(Ui.Button(row,"收回 1",()=>{S.UnlistGoods(id,1);RebuildDock();},16),76);
                }
            }
            foreach(var pcs in shop.DisplayPcs.GroupBy(t=>t))
            {
                var row=Ui.Row(shelves,44);row.name="ShelfPc_"+pcs.Key;
                Ui.Size(Ui.Label(row,PartCatalog.TierName(pcs.Key)+"整机 · 展示台",17),-1,-1,1);StockCount(row,pcs.Count());
            }
            if(shown.Count==0&&shop.DisplayPcs.Count==0)Line(shelves,"暂无已陈列商品。",16,Ui.Dim);

            Line(C,"后仓 · 容量不限",19,Ui.Accent).name="StockWarehouseHeading";
            var warehouse=Ui.Rect(C,"StockWarehouseSection");Ui.VList(warehouse,4,0);
            if(!S.HasWarehouseHere){Line(warehouse,"未摆放纸箱。",16,Ui.Dim);return;}
            int rows=0;
            foreach(var item in CollectBox())
            {
                int count=item.Count-(shown.TryGetValue(item.Key,out int front)?front:0);
                if(count<=0)continue;
                rows++;string key=item.Key;
                var row=Ui.Row(warehouse,60,4);row.name="WarehouseItem_"+key;
                Ui.Size(ProductText(row,ProductLabel(item.Name,item.Kg/item.Count),16),-1,-1,1);
                if(key=="rice")Ui.Size(Ui.Label(row,item.Kg.ToString("0.##")+" 千克",16),90).name="StockQuantity";
                else StockCount(row,count);
                if(key.StartsWith("gift:")&&ShelfKinds.ForGift(key.Substring(5))!=null)
                    Ui.Size(Ui.Button(row,"上架",()=>{S.ListGoods(key.Substring(5),count);RebuildDock();},16,Ui.ButtonHot),64);
                if(key.StartsWith("part:")&&key.EndsWith(":b"))
                {
                    var part=PartCatalog.Get(key.Split(':')[1]);
                    Ui.Size(Ui.Button(row,"拆 1",()=>{S.Unbox(part,1);RebuildDock();},16),64);
                    Ui.Size(Ui.Button(row,"全拆",()=>{S.Unbox(part,count);RebuildDock();},16),64);
                }
                Ui.Size(Ui.Button(row,"拿 1",()=>
                {
                    if(key.StartsWith("stockgift:"))S.TakeShopGoodsIntoBag(key.Substring(10),1);
                    else S.TakeFromBox(key,1);
                    RebuildDock();
                },16),64);
            }
            if(rows==0)Line(warehouse,"后仓是空的。",16,Ui.Dim);
        }

        private static void StockCount(Transform parent,int count)=>Ui.Size(Ui.Label(parent,"×"+count,17,TextAnchor.MiddleLeft),64).name="StockQuantity";

        // ---------- 定价 ----------

        private void BuildPricing()
        {
            if(S.IsSpecialBusiness(S.CurrentShop))
            {
                BuildSpecialCounter();
                if(S.RetailKind=="restaurant")BuildRestaurantDrinkPricing();
                return;
            }
            var shop=S.PlayerShop;
            var fills=ShelfKinds.All.Select(shop.ShelfFillOf).ToArray();
            var parts=fills.SelectMany(f=>f.Parts).Where(l=>l.Count>0).ToArray();
            var goods=fills.SelectMany(f=>f.Goods).Where(l=>l.Value>0).ToArray();
            Line(C,RetailShopTypes.Name(S.RetailKind)+" · 仅列货架上已陈列的商品",17,Ui.Accent);
            Line(C,"可逐件调价，点“确定”生效；未单独定价的商品跟随统一加价。",15,Ui.Dim);
            var global=Line(C,"",19,Ui.Good);
            global.name="PricingPurchaseIntentSummary";
            global.Tip("购买意愿按货架上已陈列的商品件数汇总，包含每件商品的实际定价、销售员和镜子加成。具体商品以各自的购买意愿为准；季节和预算仍会影响实际成交。");
            var refreshers=new List<System.Action>();
            void RefreshAll()
            {
                long count=goods.Sum(g=>(long)g.Value)+parts.Sum(p=>(long)p.Count);
                double total=goods.Sum(g=>g.Value*shop.GoodsPurchaseIntentAtPrice(g.Key,shop.GoodsPriceOf(g.Key).Yuan))
                    +parts.Sum(p=>p.Count*shop.PartPurchaseIntentAtPrice(p.Part,p.Boxed,shop.PriceOf(p.Part,p.Boxed).Yuan));
                global.text=$"统一加价 {shop.Markup:0.00}× · "+(count>0?$"参考购买意愿 {total/count*100:0}%":"购买意愿：暂无商品");
                foreach(var refresh in refreshers)refresh();
            }
            Ui.Slider(C,(float)ShopSettings.MinMarkup,(float)ShopSettings.MaxMarkup,(float)shop.Markup,v=>{S.SetMarkup(Mathf.Round(v*20)/20f);RefreshAll();}).name="GlobalMarkupSlider";
            var list=Ui.Rect(C,"PricingProducts");Ui.VList(list,4,0);
            void ProductRow(string key,string name,double kg,int count,System.Func<Money> price,System.Func<double> intent,
                System.Action edit,System.Func<bool> own,System.Action follow)
            {
                var row=Ui.Row(list,86,6);row.name="Pricing_"+key;
                Ui.Size(ProductText(row,ProductLabel(name,kg,$"已陈列 {count} 件"),16),-1,-1,1);
                var current=Ui.Size(Ui.Label(row,"",16,TextAnchor.MiddleLeft,Ui.Good),104);
                var editButton=Ui.Size(Ui.Button(row,"调价",edit,16,Ui.ButtonHot),62,38);editButton.name="EditPrice_"+key;
                var followButton=Ui.Size(Ui.Button(row,"跟随",()=>{follow();RefreshAll();},15),56,38);
                void Refresh(){current.text=price()+$"\n<size=14>购买意愿 {intent()*100:0}%</size>";followButton.interactable=own();}
                refreshers.Add(Refresh);
            }
            foreach(var item in goods)
            {
                string id=item.Key;var product=Gifts.Get(id);
                ProductRow("goods_"+id,product.Name,product.WeightKg,item.Value,()=>shop.GoodsPriceOf(id),
                    ()=>shop.GoodsPurchaseIntentAtPrice(id,shop.GoodsPriceOf(id).Yuan),
                    ()=>OpenWindow("调价 · "+product.Name,600,300,w=>
                    {
                        Para(w.Content,product.Name+" · 调整时预览购买意愿，确定后保存售价。",17);
                        Ui.Size(ShelfSaleTag(w,w.Content,"goods_"+id,shop.GoodsPriceOf(id).Yuan,shop.GoodsPriceRange(id),
                            value=>shop.GoodsPurchaseIntentAtPrice(id,value),value=>{shop.SetGoodsSalePrice(id,value);RebuildDock();}),-1,100);
                    }),()=>shop.HasGoodsMarkup(id),()=>shop.SetGoodsMarkup(id,null));
            }
            foreach(var item in parts)
            {
                var line=item;var part=line.Part;bool boxed=line.Boxed;string key=part.Id+(boxed?"_boxed":"_loose");
                ProductRow(key,part.Name+(boxed?" · 盒装":" · 散件"),PartUnitKg(part,boxed),line.Count,()=>shop.PriceOf(part,boxed),
                    ()=>shop.PartPurchaseIntentAtPrice(part,boxed,shop.PriceOf(part,boxed).Yuan),
                    ()=>OpenWindow("调价 · "+part.Name,600,300,w=>
                    {
                        Para(w.Content,part.Name+" · "+(boxed?"盒装":"散件"),17);
                        Ui.Size(ShelfSaleTag(w,w.Content,key,shop.PriceOf(part,boxed).Yuan,shop.PartPriceRange(part,boxed),
                            value=>shop.PartPurchaseIntentAtPrice(part,boxed,value),value=>{shop.SetPartSalePrice(part,boxed,value);RebuildDock();}),-1,100);
                    }),()=>shop.HasPartMarkup(part),()=>shop.SetPartMarkup(part,null));
            }
            if(goods.Length==0 && parts.Length==0)Line(list,"货架上暂无商品，陈列后可在这里调价。",17,Ui.Dim);
            RefreshAll();
        }

        // ---------- 经营报表 ----------

        /// <summary>近 14 天每天的营业额（绿）和支出（红）柱状图。</summary>
        public void OpenReportWindow()
        {
            OpenWindow("经营报表", 900, 640, w =>
            {
                var days = PCShopSell.Core.Finance.FinanceReport.Daily(S.Ledger, S.Clock.Today, 14);
                Money rev = Money.Zero, exp = Money.Zero, other = Money.Zero;
                long max = 100;
                foreach (var d in days)
                {
                    rev += d.Revenue;
                    exp += d.Expense;
                    other += d.OtherIncome;
                    max = System.Math.Max(max, System.Math.Max(d.Revenue.Cents, d.Expense.Cents));
                }
                Line(w.Content, $"近 {days.Count} 天：营业额 {rev}　支出 {exp}　现金变化 {rev + other - exp}", 19);
                Line(w.Content, "<color=#A9C9A9>■</color> 营业额（零售 + 装机）　<color=#D79780>■</color> 支出（进货、房租、工资、开销等）", 15, Ui.Dim);

                Line(w.Content, $"其他收款 {other}（含退押金等）；采购、押金、家具和生活开销计入现金支出，不代表利润。", 14, Ui.Dim);
                Image plot = Ui.Panel(w.Content, "Chart", new Color(0, 0, 0, 0.22f));
                Ui.Size(plot, -1, 380);
                var h = plot.gameObject.AddComponent<HorizontalLayoutGroup>();
                h.padding = new RectOffset(8, 8, 8, 8);
                h.spacing = 4;
                h.childControlWidth = true;
                h.childControlHeight = true;
                h.childForceExpandWidth = false;
                h.childForceExpandHeight = true;
                h.childAlignment = TextAnchor.LowerLeft;
                foreach (var d in days)
                {
                    RectTransform col = Ui.Rect(plot.transform, "Day");
                    Ui.Size(col, 56, -1, 0);
                    RectTransform area = Ui.Rect(col, "Bars").Anchor(0, 0, 1, 1, 0, 40, 0, 0);
                    float hr = d.Revenue.Cents <= 0 ? 0 : Mathf.Max(0.01f, (float)d.Revenue.Cents / max);
                    float he = d.Expense.Cents <= 0 ? 0 : Mathf.Max(0.01f, (float)d.Expense.Cents / max);
                    Ui.Panel(area, "Rev", new Color(0.48f, 0.89f, 0.55f)).rectTransform.Anchor(0.05f, 0, 0.48f, hr);
                    Ui.Panel(area, "Exp", new Color(0.88f, 0.5f, 0.42f)).rectTransform.Anchor(0.52f, 0, 0.95f, he);
                    Text lbl = Ui.Label(col, $"{d.Date.Month}/{d.Date.Day}\n<size=11><color=#A9C9A9>{(d.Revenue.IsZero ? "-" : "¥" + d.Revenue.Yuan)}</color></size>\n<size=11><color=#D79780>{(d.Expense.IsZero ? "-" : "¥" + d.Expense.Yuan)}</color></size>", 12, TextAnchor.UpperCenter);
                    lbl.rectTransform.Anchor(0, 0, 1, 0, 0, 0, 0, -40);
                }

                // 最好的一天
                var best = days.OrderByDescending(x => x.Revenue.Cents).FirstOrDefault();
                if (best != null && best.Revenue.IsPositive) Line(w.Content, $"营业额最高：{best.Date.Month}/{best.Date.Day}，营业额 {best.Revenue}", 16, Ui.Dim);

            });
        }

        // ---------- 订单 ----------

        private void BuildOrders()
        {
            ShopSimulation shop = S.PlayerShop;
            if (shop.IsGoodsShop) { Line(C, "这家店经营零售商品，不接电脑装机订单。", 18, Ui.Dim); return; }
            if (!shop.BuildOrdersUnlocked) Line(C, "开局前 14 天不接顾客装机订单，第 15 天开始开放。", 17, Ui.Accent);
            Line(C, "顾客下的订单都在这里（目前是装机订单）：两天内做好交付才收钱，超时取消、掉知名度。配件从库存里出。", 15, Ui.Dim);
            RectTransform list = Ui.Scroll(C, 430);
            foreach (BuildOrder o in shop.Orders)
            {
                BuildOrder order = o;
                List<PartDef> missing = S.MissingParts(o);
                long left = S.Clock.Now.MinutesUntil(o.DueAt);
                Text head = Ui.Label(list, $"<b>{PartCatalog.TierName(o.Tier)}整机</b>  报价 <color=#A9C9A9>{o.Price}</color>  剩 {left / 60} 小时", 18);
                Ui.Size(head, -1, 28);
                foreach (PartDef p in PartCatalog.BuildList(o.Tier))
                {
                    bool have = order.PartsReserved || shop.Stock.CountAll(p) > 0;
                    Ui.Size(Ui.Label(list, $"   {(have ? "<color=#A9C9A9>✓</color>" : "<color=#D79780>✗</color>")} {PartCatalog.CategoryName(p.Category)}：{p.Name}", 16), -1, 22);
                }
                Ui.Size(Ui.Button(list, missing.Count == 0 ? "开始装机" : $"还缺 {missing.Count} 样配件", () => OpenBuildMinigame(order), 18, Ui.ButtonHot, missing.Count == 0 && order.EmployeeMinutesLeft == 0), -1, 40);
                OrderActions(list, order, RebuildDock);
            }
            if (shop.Orders.Count == 0) Ui.Size(Ui.Label(list, "暂时没有订单。部分顾客想要装整机，装机订单会出现在这里。", 17, TextAnchor.MiddleLeft, Ui.Dim), -1, 30);
        }

        // ---------- 升级 ----------

        private static string PaymentText(PaymentMethods m)
        {
            if ((m & PaymentMethods.SelfCheckout) != 0) return "自助结账，自动收款";
            if ((m & PaymentMethods.QrCode) != 0) return "现金 + 收款码";
            return "只收现金";
        }

        private void BuildUpgrades()
        {
            Para(C, "店里还能添的设备。这里只是清单，要去对应的店里买，买了当天装好，搬店也跟着走。", 16, Ui.Dim);
            Para(C, $"现在：{GameSession.SizeName(S.PlayerShop.Size)}，月租 {S.CurrentShopRent}。想换大铺子或者换个好位置：去「安居房产中介」再签一间，打电话叫货车把货搬过去，再回这里退租。", 15, Ui.Accent);
            if (S.CurrentShop.Rented)
            {
                OwnedShop shop = S.CurrentShop;
                string why = S.CloseShopBlocker(shop);
                var close = Ui.Button(C, shop.OwnsProperty?"结束这家店的自营…":$"退租这家店（押金 {shop.Lease.Deposit} 不退）…", () => ConfirmCloseShop(shop), 16, Ui.ButtonBg);
                close.name = "CloseShop";
                Ui.Size(close, -1, 40);
                if(why!=null)Line(C,"退租前需处理："+why,15,Ui.Dim);
            }
            RectTransform list = Ui.Scroll(C, 400);
            foreach (ShopUpgradeDef u in ShopUpgrades.All.Where(x => x.Offered))
            {
                bool owned = S.HasUpgrade(u.Id);
                string blocker = owned ? null : S.UpgradeBlocker(u);
                string status = owned ? "<color=#A9C9A9>已装好</color>"
                    : $"{u.Price}　去<color=#DDC18B>{u.SoldAtName}</color>买" + (blocker != null && blocker != "钱不够" ? $"　<color=#D79780>{blocker}</color>" : "");
                Para(list, $"<b>{u.Name}</b>　{status}\n<size=14>{u.Description}</size>", 17);
            }
            Para(list, "智能手机、收款码、自助结账、监控在街上的手机店；招牌灯箱、空调和更大更结实的货架在宜居家具城。自助结账一年目标达标也会赠送。", 14, Ui.Dim);
        }

        // ---------- 招人 ----------

        private long _staffRefreshMinute = -1;
        private int _staffCandidateCount;
        private string _staffRosterSignature;
        private string StaffRosterSignature()=>string.Join(";",S.EmployeesForShop(S.CurrentShop).Select(r=>r.Person.Id+":"+r.Role+":"+r.Shop?.Place.Id
            +":"+r.Person.ShiftStart+":"+r.Person.ShiftEnd+":"+r.Person.DailyWageYuan+":"+r.Person.TransferShopId+":"+S.EmployeeStatus(r)));
        private void BuildStaff()
        {
            _staffRefreshMinute = S.Clock.Now.TotalMinutes;
            _staffCandidateCount = S.Candidates.Count;
            _staffRosterSignature=StaffRosterSignature();
            Line(C,GameSession.SizeName(S.PlayerShop.Size)+" · 最多 "+S.ShopStaffLimit+" 名员工 · 已占用 "+S.StaffCommitted(S.CurrentShop)+" 个名额",18,Ui.Accent).name="RecruitmentShopCapacity";
            Line(C,"待安排员工和已预留调入也占名额；同一岗位可安排多人。",15,Ui.Dim);
            var roster=S.EmployeesForShop(S.CurrentShop);
            Line(C,"本店员工",19,Ui.Accent);
            if(roster.Length==0)Line(C,"本店还没有员工。",17,Ui.Dim);
            foreach(var assignment in roster)
            {
                var worker=assignment.Person;string personId=S.PersonIdOf(worker);
                bool incoming=assignment.Shop!=S.CurrentShop&&worker.TransferShopId==S.CurrentShop.Place.Id;
                string role=GameSession.RoleName(incoming?S.PlannedEmployeeRole(assignment):assignment.Role);
                string state=incoming?"明日调入 · 当前"+(assignment.Shop==null?"待安排":S.ShopTitle(assignment.Shop)):ShopEmployeeWorkText(assignment);
                var row=Ui.Row(C,108,8);row.name="ShopEmployee_"+worker.Id;
                PortraitCard(row,personId,worker.Name,"",64,()=>OpenPersonDialogue(personId),tagText:null);
                var nextShop=S.ShopAt(worker.TransferShopId);
                string next=nextShop!=null&&!incoming?"\n明日调往："+S.ShopTitle(nextShop):"";
                var card=Ui.Size(Ui.Button(row,"<b>"+worker.Name+"</b> · "+role+"\n"+state+"\n班次 "+Ui.Hm(worker.ShiftStart)+"—"+Ui.Hm(worker.ShiftEnd)+" · 日薪 "+worker.DailyWage+next,
                    ()=>OpenPersonDialogue(personId),16,Ui.ButtonBg),-1,-1,1);
                card.name="ShopEmployeeChat_"+worker.Id;
            }
            Ui.Size(Ui.Button(C,"管理岗位与排班",OpenEmployeesPanel,17),-1,38);
            Line(C,"招聘",19,Ui.Accent);
            Ui.Size(Ui.Button(C, S.JobAdDay >= 0 ? "招聘告示已贴，明天 9:00 再来一批人" : $"再贴招聘告示（{GameSession.JobAdFee}）", () =>
            {
                S.PostJobAd(); RebuildDock();
            }, 18, Ui.ButtonHot, S.JobAdDay < 0), -1, 44);
            if (S.Candidates.Count > 0)
            {
                Line(C, "来应聘的人：", 19);
                for (int i = 0; i < S.Candidates.Count; i++)
                {
                    int idx = i;
                    Employee c = S.Candidates[i];
                    RectTransform row = Ui.Row(C, 144);
                    Ui.Size(Ui.Label(row, $"{c.Name} · 日薪 {c.DailyWage}\n<size=14>{c.Description}\n{GameSession.EmployeeAbilities(c)}\n<color=#E7B878>{S.CandidateWaitText(c)}</color></size>", 16), -1, -1, 1);
                    Ui.Size(Ui.Button(row, "聘用并安排工作", () =>
                    {
                        OpenRecruitWork(c);
                    }, 17, Ui.ButtonHot), 180);
                    Ui.Size(Ui.Button(row,"交谈",()=>OpenPersonDialogue(S.PersonIdOf(c)),17),90);
                }
                Line(C, "应聘者只等到当天 18:00，过时离开。", 15, Ui.Warn);
                Line(C, "确认前不会聘用；已聘用的待安排员工仍按日发薪。", 14, Ui.Dim);
                return;
            }

        }

        private string ShopEmployeeWorkText(EmployeeAssignment row)
        {
            if(row.Role==EmployeeRole.Unassigned||!S.CityEmployeeAvailable(row.Person,row.Shop))return S.EmployeeStatus(row);
            switch(row.Role)
            {
                case EmployeeRole.Cashier:return row.Shop.Sim.Queue.Count>0?"在岗 · 接待结账（"+row.Shop.Sim.Queue.Count+" 人排队）":"在岗 · 收银值守，等待顾客";
                case EmployeeRole.Assembler:return !row.Shop.AutoBuild?"在岗 · 自动装机未开启":row.Shop.Sim.Orders.Count>0?"在岗 · 装机值守（店内 "+row.Shop.Sim.Orders.Count+" 单待处理）":"在岗 · 等待装机订单";
                case EmployeeRole.Salesperson:return "在岗 · 导购与销售接待";
                case EmployeeRole.Buyer:
                    var trip=S.EmployeePurchaseTrips.FirstOrDefault(t=>t.EmployeeId==row.Person.Id);
                    return trip!=null?S.EmployeePurchaseTripStatus(trip):row.Person.PurchasePlan?.Daily==true?"在店待命 · 等待下次采购":"在店待命 · 尚未启用定期采购";
                default:return S.EmployeeStatus(row);
            }
        }
    }
}
