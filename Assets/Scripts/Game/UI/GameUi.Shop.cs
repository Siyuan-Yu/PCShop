using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
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
        private Text _dockTitle, _dockStatus;
        private Image _dockStatusPlate;
        private GameObject _staffPurchaseAlert;
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
            bool visible=inside&&!_ctrl.Placing&&!_ctrl.PetFocusActive&&!S.IsGameOver&&!MainMenuOpen;
            if(_shopDockToggle!=null)_shopDockToggle.gameObject.SetActive(visible);
            if(!inside){CloseDock();_dockShopId=null;_dockCollapsed=false;return;}
            if(_dockShopId!=S.CurrentShop.Place.Id){_dockShopId=S.CurrentShop.Place.Id;_dockCollapsed=false;}
            if(!visible){CloseDock();return;}
            if(!_dockCollapsed&&!DockOpen)OpenDock();
            if(_shopDockToggle!=null)_shopDockToggle.GetComponentInChildren<Text>().text=DockOpen?"收起店铺面板 ▴":"展开店铺面板 ▾";
        }

        public bool DockOpen => _dock != null;
        internal const float ShopDockWidth = 600f;

        public void OpenDock()
        {
            if(S.IsOwnedVenue(S.CurrentPlace)){OpenVenueManagement(S.CurrentPlace);return;}
            if(!S.InPlayerShop)return;
            _dockCollapsed=false;_dockShopId=S.CurrentShop.Place.Id;
            if (_dock != null) return;
            Image panel = Ui.FlatPanel(_dockCanvas.transform, "ShopDock", Ui.FlatWindow);
            _dock = panel.rectTransform.Anchor(1, 0, 1, 1, -ShopDockWidth, 70, 10, 120);

            var titleRow = Ui.Row(_dock, 40, 10).Anchor(0, 1, 1, 1, 16, -49, 120, 6);
            titleRow.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
            _dockTitle = Ui.Size(Ui.Label(titleRow, "", 23), -1, 40);
            _dockTitle.fontStyle = FontStyle.Bold;
            _dockTitle.resizeTextForBestFit = true;
            _dockTitle.resizeTextMinSize = 16;
            _dockTitle.resizeTextMaxSize = 23;
            _dockTitle.verticalOverflow = VerticalWrapMode.Truncate;
            var rename = Ui.Size(Ui.Button(titleRow, "改名", () => OpenShopRename(S.CurrentShop), 16), 76, 34);
            rename.name = "RenameShop";
            rename.GetComponent<LayoutElement>().minWidth = 76;
            _dockStatusPlate = Ui.Panel(_dock, "ShopOpenStatus", Ui.SuccessSurface);
            _dockStatusPlate.rectTransform.Anchor(1, 1, 1, 1, -117, -40, 15, 12);
            _dockStatusPlate.raycastTarget = false;
            _dockStatus = Ui.Label(_dockStatusPlate.transform, "", 15, TextAnchor.MiddleCenter);
            _dockStatus.rectTransform.Fill(2);

            RectTransform tabs = Ui.Rect(_dock, "Tabs").Anchor(0, 1, 1, 1, 12, -93, 12, 52);
            var h = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 4;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = true;
            _tabButtons.Clear();
            foreach (var (tab, name) in Tabs)
            {
                ShopTab t = tab;
                var button = Ui.Button(tabs, name, () =>
                {
                    if (t == ShopTab.Staff) S.VisitOpeningStaffTab();
                    if (t == ShopTab.Stock) S.VisitOpeningShelfStock();
                    if (t == ShopTab.Renovate) S.VisitOpeningRenovationTab();
                    if (t == ShopTab.Counter) S.VisitOpeningCounterTab();
                    SelectTab(t);
                }, 17);
                Ui.Trim((RectTransform)button.transform, "ActiveTabRule", Ui.Good, 0);
                _tabButtons.Add((button, t));
                if (t == ShopTab.Staff) _staffPurchaseAlert = AddPurchaseAlertDot(button.transform);
            }
            Ui.Panel(_dock, "TabRule", Ui.Edge).rectTransform.Anchor(0, 1, 1, 1, 12, -99, 12, 98);

            _dockContent = Ui.Scroll(_dock);
            ((RectTransform)_dockContent.parent.parent).Anchor(0, 0, 1, 1, 8, DockContentBottom, 5, 103);

            Button leave = Ui.Button(_dock, "离开店铺", ConfirmLeaveShop, 16);
            ((RectTransform)leave.transform).Anchor(1, 0, 1, 0, -146, 8, 12, -44);
            BuildShopFitWarning();

            SelectTab(_tab);
        }

        private Window _leaveConfirm;

        /// <summary>离店会导致关门时先确认，否则直接离店并提示营业状态。</summary>
        public void ConfirmLeaveShop()
        {
            if (_leaveConfirm != null) return;
            if (S.LeaveBlocker != null)
            {
                Toast(S.LeaveBlocker);
                return;
            }
            var status = S.ShopLeaveStatus();
            if (!status.WillClose)
            {
                _ctrl.LeaveShop();
                if (!S.InPlayerShop) Toast(status.Message + "。");
                return;
            }
            int waiting = S.PlayerShop.Queue.Count;
            _leaveConfirm = OpenWindow("要离开店铺吗？", 620, 320, w =>
            {
                Para(w.Content, status.Message + "。", 19, Ui.Warn);
                if (waiting > 0)
                    Para(w.Content, $"还有 {waiting} 位顾客在排队。关门后他们会离开。", 17, Ui.Dim);
                RectTransform row = Ui.Row(w.Content, 50, 12);
                Ui.Size(Ui.Button(row, "再待会儿", w.Close, 19), -1, -1, 1);
                Ui.Size(Ui.Button(row, "关门离开", () =>
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
            _staffPurchaseAlert = null;
            _counterRefresh.Clear();
            _staffRefresh.Clear();
            _refreshPricing = null;
            _pricingShop = null;
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
            foreach (var (b, t) in _tabButtons)
            {
                bool selected = t == tab;
                Ui.SetButtonFill(b, selected ? Ui.FlatRow : Ui.FlatWindow);
                b.transform.Find("ActiveTabRule").gameObject.SetActive(selected);
            }
            RefreshDockHeader();
            RebuildDock();
        }

        private void RefreshDockHeader()
        {
            _dockTitle.text = S.ShopTitle(S.CurrentShop);
            bool open = S.IsPlaceOpen(S.CurrentShop.Place);
            bool finishing = !open && S.ShopFinishingService(S.CurrentShop);
            _dockStatus.text = open ? "● 营业中" : finishing ? "● 打烊收尾" : "● 已关门";
            _dockStatus.color = open ? Ui.Good : finishing ? Ui.Accent : Ui.Warn;
            _dockStatusPlate.color = open ? Ui.SuccessSurface : finishing ? Ui.PendingSurface : Ui.WarningSurface;
            RefreshStaffPurchaseAlert();
        }

        private bool BuyerNeedsPurchasePlan(Employee buyer) => buyer.PurchasePlan?.Daily != true && S.EmployeeTrip(buyer.Id) == null;

        private void RefreshStaffPurchaseAlert()
        {
            bool needsPlan = S.ShopStaff(S.CurrentShop, EmployeeRole.Buyer).Any(BuyerNeedsPurchasePlan);
            _staffPurchaseAlert.SetActive(needsPlan);
        }

        private static GameObject AddPurchaseAlertDot(Transform parent)
        {
            var dot = Ui.Panel(parent, "PurchaseAlertDot", Ui.FlatDown);
            dot.sprite = CircleSprite();
            dot.raycastTarget = false;
            dot.rectTransform.Anchor(1, 1, 1, 1, -16, -16, 6, 6);
            return dot.gameObject;
        }

        private void RefreshDock()
        {
            if (_dock == null) return;
            if (!S.InPlayerShop)
            {
                CloseDock();
                return;
            }
            RefreshDockHeader();
            // Keep the current controls alive while the player is choosing or scrolling a dropdown.
            if (ListScrollbar.PressedInside(_dock) || ChoiceDropdown.OpenInside(_dock)
                || _dock.GetComponentsInChildren<InputField>().Any(input=>input.isFocused)) return;
            RefreshShopFitWarning();
            if((_tab==ShopTab.Stock || _tab==ShopTab.Pricing && _refreshPricing==null) && !AnyModalOpen && _stockViewSignature!=StockViewSignature())
            {RebuildDock();return;}
            if (_tab == ShopTab.Pricing && !AnyModalOpen && _refreshPricing != null)
            {
                if (_pricingShop != S.PlayerShop) RebuildDock();
                else _refreshPricing();
                return;
            }
            if(_tab==ShopTab.Renovate&&!AnyModalOpen&&_renovationStockSignature!=RenovationStockSignature())
            {RebuildDock();return;}
            if (_tab == ShopTab.Counter || (_tab==ShopTab.Orders&&(S.IsSpecialBusiness(S.CurrentShop)||S.IsPetShop(S.CurrentShop))))
            {
                if (_counterStructure != CounterStructure()) RebuildDock();
                else foreach (var refresh in _counterRefresh) refresh();
            }
            else if (_tab == ShopTab.Staff)
            {
                if (_staffStructure != StaffStructure(S.EmployeesForShop(S.CurrentShop))) RebuildDock();
                else foreach (var refresh in _staffRefresh) refresh();
            }
            else if (_tab == ShopTab.Orders && !AnyModalOpen)
            {
                if (!S.PlayerShop.IsGoodsShop && _buildOrdersSignature != BuildOrdersSignature()) RebuildDock();
                else foreach (var refresh in _counterRefresh) refresh();
            }
        }

        private string _counterStructure;
        private string _buildOrdersSignature;
        private string BuildOrdersSignature() => string.Join(";", S.PlayerShop.Orders.Select(o =>
            $"{o.Id}:{S.PlayerShop.MissingParts(o).Count}:{o.EmployeeMinutesLeft > 0}"));
        private readonly List<System.Action> _counterRefresh = new List<System.Action>();
        private string CounterStructure()
        {
            var shop = S.PlayerShop;
            // Rows/buttons only rebuild on structural changes; ticking numbers keep their Text objects.
            return S.CurrentShop.Place.Id + "|" + S.RetailKind + "|" + S.Clock.Today.TotalDays
                + "|" + S.ShopFitGap() + "|" + S.IsRenovating + "|" + S.CurrentShop.OwnsProperty
                + (S.IsPetShop(S.CurrentShop)
                    ? "|" + _ctrl.Interior?.HasCounter + "|"
                        + (_tab == ShopTab.Orders ? PetOrdersSignature(S.CurrentShop) : PetCounterSignature(S.CurrentShop)) : "")
                + "|" + shop.OpenMinute + ":" + shop.CloseMinute + ":" + shop.InHours(S.Clock.Now) + ":" + shop.Accepted
                + "|" + S.ShopFinishingService(S.CurrentShop)
                + "|" + S.Employee?.Id + ":" + (S.Employee != null && S.CityEmployeeAvailable(S.Employee, S.CurrentShop))
                + "|" + S.Salesperson?.Id + ":" + (S.Salesperson != null ? GameSession.EmployeeCharm(S.Salesperson) : 0) + ":" + shop.SalesPurchaseIntentBonus
                + "|" + shop.ClothingMirrorPurchaseIntentBonus + "|" + shop.Orders.Count
                + "|" + string.Join(",", shop.Queue.Select(c => c.Id))
                + (S.IsSpecialBusiness(S.CurrentShop) ? "|" + SpecialCounterStructure() : "");
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
            string patience = seconds.HasValue ? (seconds.Value >= 12 ? $"<color=#9C4634>已等 {seconds.Value:0} 秒，请尽快结账</color>" : $"已等 {seconds.Value:0} 秒") : $"等了 {waited} 游戏分钟";
            return $"{customer.ItemName}（{(customer.DisplayTier >= 0 ? "展示机" : customer.GoodsId != null ? "杂货" : customer.ItemBoxed ? "盒装" : "散件")}） <color=#386047>{customer.Spend}</color>\n<size=14>{patience}</size>";
        }

        private string CounterCheckoutHint() => _ctrl.CheckoutPending ? "正在前往收银台，WASD / 方向键可接管"
            : _ctrl.Interior?.NearCounter == true ? "按 F 或点「结账」收款" : "点「结账」，角色会走到收银台收款";

        private ShopTab? _scrollTab;
        private string _stockViewSignature;
        private string StockViewSignature()=>S.CurrentShop.Place.Id+"|"+S.RetailKind+"|"
            +string.Join(";",S.PlayerShop.Stock.Lines().Select(l=>l.Part.Id+":"+l.Boxed+":"+l.Count))+"|"
            +string.Join(";",S.PlayerShop.Stock.GoodsLines().Select(l=>l.Key+":"+l.Value))+"|"+S.HasWarehouseHere+"|"
            +string.Join(";",S.ShopLayout.Select(p=>p.Id+":"+p.Item+":"+p.DisplayLimit))+"|"
            +string.Join(";",CollectBox().Select(b=>b.Key+":"+b.Count+":"+b.Kg))+"|"+string.Join(",",S.PlayerShop.DisplayPcs)
            +(S.RetailKind=="restaurant"?"|fridge:"+string.Join(";",CollectStacks(S.CurrentShop.Fridge,new Stock(0)).Select(b=>b.Key+":"+b.Count+":"+b.Kg)):"")
            +(S.RetailKind=="secondhand"?"|used:"+string.Join(";",S.SpecialBusiness(S.CurrentShop.Place.Id).Used.Select(i=>
                $"{i.Id}:{i.Owned}:{i.Inspected}:{i.Listed}:{i.Sold}:{i.Price}:{i.Condition}:{i.RepairWorkRemaining > 0}"))
                +"|"+string.Join(",",S.PlayerShop.Queue.Select(c=>c.ResaleId))
                +$"|{S.PlayerShop.SalesPurchaseIntentBonus}|{S.PlayerShop.ReputationPurchaseIntentBonus}":"");

        public void RebuildDock()
        {
            if (_dock == null) return;
            RefreshStaffPurchaseAlert();
            var scrolled = _scrollTab == _tab && S.OpeningStaffStep == OpeningStaffLesson.None ? Ui.SaveScroll(_dock) : null;
            _scrollTab = _tab;
            _counterRefresh.Clear();
            _staffRefresh.Clear();
            _refreshPricing = null;
            _pricingShop = null;
            Ui.Clear(_dockContent);
            foreach (var (button, tab) in _tabButtons)
            {
                button.GetComponentInChildren<Text>().text = tab == ShopTab.Orders && S.RetailKind == "secondhand"
                    ? "记录" : Tabs.First(t => t.tab == tab).name;
            }
            S.VisitQuestTarget(TargetFor(_tab));
            switch (_tab)
            {
                case ShopTab.Counter: BuildCounter(); break;
                case ShopTab.Stock: BuildStock(); break;
                case ShopTab.Pricing: BuildPricing(); break;
                case ShopTab.Orders:
                    if (S.RetailKind != "restaurant") BuildIndustryReferralOrders(C, S.CurrentShop);
                    if (S.IsPetShop(S.CurrentShop)) BuildPetOrders();
                    else if (S.IsSpecialBusiness(S.CurrentShop)) BuildSpecialOrders();
                    else BuildOrders();
                    break;
                case ShopTab.Renovate: BuildRenovate(); break;
                case ShopTab.Staff: BuildStaff(); break;
            }
            RefreshShopFitWarning();
            Ui.RestoreScroll(_dock, _dock, scrolled);
            if (scrolled == null) _dockContent.anchoredPosition = Vector2.zero;
            if(_tab==ShopTab.Stock || _tab==ShopTab.Pricing && _refreshPricing==null)_stockViewSignature=StockViewSignature();
            if(_tab==ShopTab.Counter || (_tab==ShopTab.Orders&&(S.IsSpecialBusiness(S.CurrentShop)||S.IsPetShop(S.CurrentShop))))_counterStructure=CounterStructure();
        }

        private Transform C => _dockContent;

        // ---------- 柜台 ----------

        /// <summary>柜台内直接选择开门、关门时间；不限时单独切换，不再打开设置弹窗。</summary>
        private void ShopHoursRow(ShopSimulation shop) =>
            BuildShopHoursControls(C, shop, S.CurrentShop, RebuildDock);

        private void BuildShopHoursControls(Transform parent, ShopSimulation shop, OwnedShop owner, System.Action refresh)
        {
            const int day=24*60;
            bool allDay=shop.AllDay;
            int open=allDay?ShopSimulation.DefaultOpenMinute:shop.OpenMinute;
            int close=allDay?ShopSimulation.DefaultCloseMinute:shop.CloseMinute%day;
            RectTransform row=Ui.Row(parent,36,6);
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
                    if(start==end){Toast("开门和关门不能相同；全天营业请选择“不限时”。");refresh();return;}
                    shop.SetHours(start,end==0?day:end);
                    refresh();
                });
                dropdown.name=opening?"ShopOpeningTime":"ShopClosingTime";
                Ui.Size(dropdown,145,36);
                dropdown.captionText.fontSize=15;
            }
            TimeChoice(true);TimeChoice(false);
            var unlimited=Ui.Button(row,allDay?"不限时 ✓":"不限时",()=>
            {
                if(shop.AllDay)shop.SetHours(ShopSimulation.DefaultOpenMinute,ShopSimulation.DefaultCloseMinute);
                else shop.SetHours(0,day);
                refresh();
            },15,Ui.SelectedButton(allDay));
            unlimited.name="ShopUnlimitedHours";Ui.Size(unlimited,90,36);
            if(!allDay&&close<open&&close!=0)Line(C,"跨午夜营业，次日 "+Ui.Hm(close)+" 关门。",14,Ui.Dim);
            if(!allDay&&!shop.InHours(S.Clock.Now))
                Ui.Notice(parent, S.ShopFinishingService(owner) ? "打烊收尾 · 暂停接待新客，继续服务店内顾客"
                    : $"已关门 · {Ui.Hm(shop.OpenMinute)} 开门");
        }

        private void BuildCounter()
        {
            _openingCounterButtons.Clear();
            _openingPetIntakeButton = null;
            _refreshOpeningRestaurantTables = null;
            var ownedShop = S.CurrentShop;
            bool specialBusiness = S.IsSpecialBusiness(ownedShop);
            var management = Ui.Row(C, 38, 6);
            Ui.Size(Ui.Button(management, RetailShopTypes.Name(ownedShop.RetailKind) + " ▾", OpenRetailTypeChooser, 17), -1, 38, 1)
                .name = "ShopMainCategory";
            if (ownedShop.RetailKind == "restaurant")
                Ui.Size(Ui.Button(management, "?", OpenRestaurantHelp, 17), 38, 38).Tip("餐馆经营说明");
            BuildCompetitionEntry(management);
            Ui.Size(Ui.Button(management, ownedShop.OwnsProperty ? "结束自营" : "退租店铺",
                () => ConfirmCloseShop(ownedShop), 16, specialBusiness ? Ui.Warn : Ui.ButtonBg), 110, 38)
                .name = "CloseShopCounter";
            var board = Ui.FlatPanel(C, "ShopLedgerBoard", Ui.Clipboard, Ui.FlatWellEdge).rectTransform;
            Ui.VList(board, 4, 6);
            var page = Ui.PaperPanel(board, "ShopLedgerPage", lifted: true).rectTransform;
            Ui.VList(page, 2, 5);
            var heading = Line(page, "今日经营", 17, Ui.Good);
            heading.fontStyle = FontStyle.Bold;
            var clip = Ui.Panel(board, "ShopLedgerClip", Ui.ShelfChannel);
            clip.raycastTarget = false;
            clip.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            clip.rectTransform.Anchor(.5f, 1, .5f, 1, -30, -8, -30, 0);
            Ui.Trim(clip.rectTransform, "ClipEdge", Ui.FlatWellEdge, 1, top: true);
            BuildCounterTotals(page, ownedShop);
            BuildShopStanding(page, S.PlayerShop, refreshWithCounter: true);
            if (specialBusiness) { BuildSpecialCounter(); return; }
            if (S.RetailKind == "models")
                Ui.Size(Ui.Button(C, "模型委托 · 改胶与喷漆", OpenModelCommissions, 16), -1, 36);
            BuildIndustryBrief(C);
            ShopSimulation shop = S.PlayerShop;
            ShopHoursRow(shop);
            if (S.IsPetShop(ownedShop)) BuildPetBoardingCounter(ownedShop);
            if(S.Salesperson!=null)
                PersonSummary(C, S.PersonIdOf(S.Salesperson), S.Salesperson.Name + " · 销售员",
                    shop.SalesPurchaseIntentBonus>0?$"在岗 · 购买意愿 +{shop.SalesPurchaseIntentBonus*100:0.#} 个百分点":"未在岗", 48);
            if(shop.RetailKind=="clothing" && shop.ClothingMirrorPurchaseIntentBonus>0)
                Line(C,"全身镜：服装购买意愿 +5个百分点（多面不叠加）",16,Ui.Good);
            if (S.Employee != null)
                PersonSummary(C, S.PersonIdOf(S.Employee), S.Employee.Name + " · 收银员",
                    S.CityEmployeeAvailable(S.Employee, S.CurrentShop) ? "在岗" : "未在岗", 48);

            if (shop.Queue.Count > 0)
                CounterText(CounterCheckoutHint, 17);
            RectTransform list = Ui.Rect(C, "CheckoutQueue");
            Ui.VList(list, 4, 0);
            foreach (ShopCustomer c in shop.Queue)
            {
                ShopCustomer cust = c;
                RectTransform row = Ui.Row(list, 44);
                var description = Ui.Size(Ui.Label(row, CounterQueueText(cust), 17), 0, -1, 1);
                description.verticalOverflow = VerticalWrapMode.Truncate;
                Ui.Trim(row, "CheckoutRowRule", Ui.FlatCardEdge, 0);
                var checkout = Ui.Size(Ui.Button(row, "结账", () => _ctrl.Checkout(cust.Id), 18, Ui.ButtonHot, _ctrl.CanRequestCheckout), 100);
                _openingCounterButtons[cust.Id] = checkout;
                _counterRefresh.Add(() => { description.text = CounterQueueText(cust); checkout.interactable = _ctrl.CanRequestCheckout; });
            }
            if (shop.Queue.Count == 0) Line(list, "暂无顾客排队", 16, Ui.Dim);

            int orders = shop.Orders.Count;
            if (orders > 0) Ui.Size(Ui.Button(C, $"处理 {orders} 个装机订单 →", () => SelectTab(ShopTab.Orders), 18, Ui.ButtonHot), -1, 38);

            Ui.Size(Ui.Button(C, "经营报表", OpenReportWindow, 17), -1, 38);
        }

        private void OpenShopRename(OwnedShop shop)
        {
            OpenWindow("修改店名", 520, 240, w =>
            {
                var input = Ui.Size(Ui.TextInput(w.Content, S.ShopTitle(shop), "输入新店名", null), -1, 42);
                input.characterLimit = GameSession.MaxShopNameLength;
                input.textComponent.supportRichText = false;
                var error = Ui.Size(Ui.Label(w.Content, "", 15, TextAnchor.MiddleLeft, Ui.Warn), -1, 24);
                var actions = Ui.Row(w.Content, 42, 8);
                Ui.Size(Ui.Button(actions, "取消", w.Close, 17), -1, 42, 1);
                Ui.Size(Ui.Button(actions, "保存店名", () =>
                {
                    if (!S.TryRenameShop(shop, input.text, out string reason))
                    {
                        error.text = reason;
                        return;
                    }
                    w.Close();
                    RefreshDockHeader();
                    RebuildDock();
                    RefreshShortcutMenus();
                }, 17, Ui.ButtonHot), -1, 42, 1);
            });
        }

        private void BuildCounterTotals(Transform parent, OwnedShop ownedShop)
        {
            var shop = ownedShop.Sim;
            var row = Ui.Row(parent, 78, 3);
            Metric("营业额", () =>
            {
                RefreshShopSales(ownedShop);
                return _shopSalesTotal.ToString();
            }, Ui.Good, () => OpenShopSalesDetails(ownedShop));
            Metric("进店", () => $"{ShopVisitTotal(ownedShop, false)} 人", Ui.TextColor, () => OpenShopVisitDetails(ownedShop, false),
                () => $"预计 {S.ExpectedDailyShopEntrants(ownedShop.Place):N0} 人/日");
            Metric("流失", () => $"{ShopVisitTotal(ownedShop, true)} 人", Ui.Warn, () => OpenShopVisitDetails(ownedShop, true));
            if (ownedShop.RetailKind == "restaurant")
                Metric("待收款", () => $"{S.RestaurantTablesAwaitingPayment(ownedShop)} 桌", Ui.TextColor);
            else
                Metric("排队结账", () => $"{shop.Queue.Count} 人", Ui.TextColor).Tip(() => PaymentText(shop.Accepted));

            Text Metric(string title, System.Func<string> value, Color ink, System.Action openDetails = null, System.Func<string> note = null)
            {
                bool sales = title == "营业额";
                var background = Ui.Size(Ui.Panel(row, sales ? "今日营业额" : openDetails == null ? title : "今日" + title,
                    new Color(1, 1, 1, .04f)), 0, -1, sales ? 1.23f : 1);
                var card = background.rectTransform;
                if (!sales)
                {
                    var rule = Ui.Panel(card, "MetricDivider", Ui.FlatCardEdge);
                    rule.raycastTarget = false;
                    rule.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                    rule.rectTransform.Anchor(0, 0, 0, 1, 0, 5, -1, 5);
                }
                if (openDetails != null)
                {
                    var button = card.gameObject.AddComponent<Button>();
                    button.targetGraphic = background;
                    Ui.SteadyButtonFeedback(button);
                    button.onClick.AddListener(() => openDetails());
                }
                Ui.VList(card, 1, 5);
                Ui.Size(Line(card, openDetails == null ? title : title + " ›", 15,
                    openDetails == null ? Ui.Dim : Ui.TextColor), -1, 20);
                var text = Ui.Label(card, value(), sales ? 29 : 24, color: ink);
                text.resizeTextForBestFit = true;
                text.resizeTextMinSize = 17;
                text.resizeTextMaxSize = text.fontSize;
                Ui.Size(text, -1, 28);
                text.fontStyle = FontStyle.Bold;
                _counterRefresh.Add(() => text.text = value());
                if (note != null)
                {
                    var detail = Line(card, note(), 13, Ui.Dim);
                    Ui.Size(detail, -1, 16);
                    detail.name = "ShopEntrantsEstimate";
                    detail.Tip("按营业时间、知名度、街道人流和同类店估算");
                    _counterRefresh.Add(() => detail.text = note());
                }
                return text;
            }
        }

        private readonly List<LedgerEntry> _shopSales = new List<LedgerEntry>();
        private Ledger _shopSalesLedger;
        private string _shopSalesId;
        private GameDate _shopSalesDate;
        private int _shopSalesEntryCount, _shopSalesRevision;
        private Money _shopSalesTotal;

        private void RefreshShopSales(OwnedShop shop)
        {
            var ledger = S.Ledger;
            var date = S.Clock.Today;
            int count = ledger.Entries.Count;
            // Each new ledger entry increments the revision once. A restore can replace existing rows.
            if (_shopSalesLedger != ledger || _shopSalesId != shop.SalesAccountId || _shopSalesDate != date
                || ledger.Revision - _shopSalesRevision != count - _shopSalesEntryCount)
            {
                _shopSalesLedger = ledger;
                _shopSalesId = shop.SalesAccountId;
                _shopSalesDate = date;
                _shopSalesEntryCount = 0;
                _shopSalesTotal = Money.Zero;
                _shopSales.Clear();
            }

            for (int i = _shopSalesEntryCount; i < count; i++)
            {
                var entry = ledger.Entries[i];
                if (entry.Date != date || !FinanceReport.IsShopSale(entry, shop.Place.Id, shop.SalesAccountId)) continue;
                _shopSales.Add(entry);
                _shopSalesTotal += entry.Amount;
            }
            _shopSalesEntryCount = count;
            _shopSalesRevision = ledger.Revision;
        }

        private void OpenShopSalesDetails(OwnedShop shop)
        {
            RefreshShopSales(shop);
            var entries = _shopSales.ToArray();
            var total = _shopSalesTotal;
            var date = _shopSalesDate;
            const int pageSize = 12;
            int pageCount = (entries.Length + pageSize - 1) / pageSize;
            int page = 0;
            bool animateOnOpen = true;
            OpenWindow("今日营业额明细", 760, 670, w =>
            {
                Line(w.Content, $"{S.ShopTitle(shop)} · {date}", 19, Ui.Accent);
                string FormatTotal(Money amount) => $"实收 {amount} · {entries.Length:N0} 笔";
                var totalLabel = Line(w.Content, FormatTotal(total), 24, Ui.Good);
                if (animateOnOpen)
                {
                    animateOnOpen = false;
                    var reveal = NewUiReveal();
                    reveal?.CountMoney(totalLabel, total, FormatTotal);
                    PlayUiReveal(reveal);
                }
                if (entries.Length == 0)
                {
                    Line(w.Content, "今天暂无销售收款", 17, Ui.Dim);
                    return;
                }

                if (pageCount > 1)
                {
                    var navigation = Ui.Row(w.Content, 34, 8);
                    void TurnPage(int direction)
                    {
                        page += direction;
                        w.ResetScrollOnRebuild = true;
                        w.Rebuild();
                    }
                    Ui.Size(Ui.Button(navigation, "上一页", () => TurnPage(-1), 15, interactable: page > 0), 100, 34);
                    Ui.Size(Ui.Label(navigation, $"{page + 1} / {pageCount}", 15, TextAnchor.MiddleCenter), -1, 34, 1);
                    Ui.Size(Ui.Button(navigation, "下一页", () => TurnPage(1), 15, interactable: page + 1 < pageCount), 100, 34);
                }
                const float amountWidth = 140;
                var header = Ui.Row(w.Content, 30, 8);
                Ui.Size(Ui.Label(header, "商品 / 订单", 16, color: Ui.Dim), -1, 30, 1);
                Ui.Size(Ui.Label(header, "实收金额", 16, TextAnchor.MiddleRight, Ui.Dim), amountWidth, 30);
                int last = entries.Length - 1 - page * pageSize;
                int first = Mathf.Max(0, last - pageSize + 1);
                for (int i = last; i >= first; i--)
                {
                    var entry = entries[i];
                    var row = Ui.Row(w.Content, 33, 8);
                    var item = Ui.Size(Ui.Label(row, string.IsNullOrEmpty(entry.Memo) ? entry.Category : entry.Memo, 17), -1, 33, 1);
                    item.supportRichText = false;
                    Ui.Size(Ui.Label(row, entry.Amount.ToString(), 18, TextAnchor.MiddleRight, Ui.Good), amountWidth, 33);
                }
            });
        }

        private int ShopVisitTotal(OwnedShop shop, bool lossesOnly) => shop.RetailKind == "restaurant"
            ? lossesOnly ? S.RestaurantLossesToday(shop) : S.RestaurantArrivalsToday(shop)
            : lossesOnly ? shop.Sim.TodayLost : shop.Sim.TodayCustomers;

        private void OpenShopVisitDetails(OwnedShop shop, bool lossesOnly)
        {
            var details = shop.RetailKind == "restaurant" ? S.RestaurantVisitsToday(shop, lossesOnly)
                : shop.Sim.TodayVisitDetails(lossesOnly);
            var visits = details.OrderByDescending(v => v.ArrivalMinute).ThenByDescending(v => v.CustomerId).ToArray();
            int total = ShopVisitTotal(shop, lossesOnly);
            int page = 0;
            const int pageSize = 15;
            int pageCount = Mathf.Max(1, (visits.Length + pageSize - 1) / pageSize);
            string title = lossesOnly ? "今日流失" : "今日进店";
            OpenWindow(title + "明细", 760, 670, w =>
            {
                Line(w.Content, $"{S.Clock.Today} · {title} {total:N0} 人", 21, Ui.Accent);
                if (lossesOnly && visits.Length > 0)
                {
                    string reasons = string.Join("　", visits.GroupBy(v => v.Reason).OrderByDescending(g => g.Count())
                        .Select(g => $"{ShopLossText(g.Key)} {g.Count()} 人"));
                    Para(w.Content, reasons, 16, Ui.Dim);
                }
                if (total > visits.Length)
                    Para(w.Content, $"其中 {total - visits.Length} 人只有人数记录。", 15, Ui.Dim);
                if (visits.Length == 0)
                {
                    Line(w.Content, total > 0 ? "暂无可查看的明细" : lossesOnly ? "今天暂无流失顾客" : "今天暂无顾客进店", 17, Ui.Dim);
                    return;
                }

                if (pageCount > 1)
                {
                    var navigation = Ui.Row(w.Content, 34, 8);
                    Ui.Size(Ui.Button(navigation, "上一页", () => { page--; w.Rebuild(); }, 15, interactable: page > 0), 100, 34);
                    Ui.Size(Ui.Label(navigation, $"{page + 1} / {pageCount}", 15, TextAnchor.MiddleCenter), -1, 34, 1);
                    Ui.Size(Ui.Button(navigation, "下一页", () => { page++; w.Rebuild(); }, 15, interactable: page + 1 < pageCount), 100, 34);
                }
                var header = Ui.Row(w.Content, 24, 8);
                Ui.Size(Ui.Label(header, "进店时间", 15, color: Ui.Dim), 108, 24);
                Ui.Size(Ui.Label(header, "顾客", 15, color: Ui.Dim), 150, 24);
                Ui.Size(Ui.Label(header, lossesOnly ? "离店原因" : "购买或离店情况", 15, color: Ui.Dim), -1, 24, 1);
                foreach (var visit in visits.Skip(page * pageSize).Take(pageSize))
                {
                    var time = new GameTimestamp(visit.ArrivalMinute);
                    var row = Ui.Row(w.Content, 34, 8);
                    string day = time.Date.Equals(S.Clock.Today) ? "" : $"{time.Date.Month}/{time.Date.Day} ";
                    Ui.Size(Ui.Label(row, $"{day}{time.Hour:00}:{time.Minute:00}", 16), 108, 34);
                    string name = S.CityPerson(visit.PersonId)?.Name ?? $"顾客 #{visit.CustomerId}";
                    Ui.Size(Ui.Label(row, name, 16), 150, 34);
                    string result = visit.StatusText ?? (visit.State switch
                    {
                        CustomerState.Paid => $"已购买 {visit.ProductName} · {Money.FromCents(visit.PaidCents)}",
                        CustomerState.WaitingCheckout => $"排队结账 · {visit.ProductName}",
                        CustomerState.Browsing => "正在选购",
                        _ when visit.Reason == LeaveReason.OrderPlaced => $"已下单 {visit.ProductName} · 待交付",
                        _ when !visit.CountedAsLost && visit.Reason == LeaveReason.NoStock => "商品已过期 · 未成交",
                        _ => $"已离店 · {ShopLossText(visit.Reason)}",
                    });
                    Color ink = visit.State == CustomerState.Paid ? Ui.Good : visit.CountedAsLost ? Ui.Warn : Ui.Dim;
                    Ui.Size(Ui.Label(row, result, 16, color: ink), -1, 34, 1);
                }
            });
        }

        private static string ShopLossText(LeaveReason reason) => reason switch
        {
            LeaveReason.Impatient => "等候结账超时",
            LeaveReason.MealWaitTimeout => "等餐超时",
            LeaveReason.MenuUnavailable => "当前无供应菜品",
            LeaveReason.NoCash => "没有可用的付款方式",
            LeaveReason.ShopClosed => "店铺停止营业",
            LeaveReason.NoStock => "没有合适的现货",
            LeaveReason.TooExpensive => "价格太高",
            LeaveReason.OverBudget => "超出顾客预算",
            _ => "未成交",
        };

        private void BuildShopStanding(Transform parent, ShopSimulation shop, bool refreshWithCounter = false)
        {
            var row = Ui.Row(parent, 84, 8);
            Text awareness = StandingCard(row, "知名度", out Text awarenessNote, out GameObject awarenessPresence);
            awareness.name = "ShopStandingLine";
            Text reputation = StandingCard(row, "口碑", out Text reputationNote, out GameObject reputationPresence);
            reputation.name = "ShopReputationLine";
            void Refresh()
            {
                awareness.text = $"{shop.Awareness:0.0}";
                double trafficBonus = (shop.StandingArrivalMultiplier - 1) * 100;
                awarenessNote.text = $"进店客流 {trafficBonus:+0.#;-0.#;0}%";
                reputation.text = $"{shop.EffectiveShopReputation:0.0}";
                reputationNote.text = $"购买意愿 {shop.ReputationPurchaseIntentBonus * 100:+0.#;-0.#;0} 个百分点";
                awarenessPresence.SetActive(shop.OwnerOnSite);
                reputationPresence.SetActive(shop.OwnerOnSite);
            }
            awareness.Tip(() => $"知名度带来 {shop.Awareness:+0.#;-0.#;0}% 客流。"
                + (shop.OwnerOnSite ? $"\n玩家在店时，进店客流再乘 {shop.OwnerTrafficMultiplier:0.##}。" : ""));
            reputation.Tip(() => $"基础口碑 {shop.ShopReputation:0.0}。"
                + (shop.OwnerOnSite ? $"\n玩家在店时，本店有效口碑升至 {shop.EffectiveShopReputation:0.0}。" : ""));
            Refresh();
            if (refreshWithCounter) _counterRefresh.Add(Refresh);

            Text StandingCard(Transform container, string title, out Text note, out GameObject presence)
            {
                var card = Ui.Size(Ui.Panel(container, "Standing_" + title, Ui.PanelBg2), -1, -1, 1).rectTransform;
                Ui.VList(card, 0, 5);
                var header = Ui.Row(card, 26, 4);
                header.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
                Ui.Size(Ui.Label(header, title, 14, color: Ui.Dim), 0, 26, 1);
                var badge = Ui.Size(Ui.FlatPanel(header, "OwnerOnSiteBonus", Ui.SuccessSurface,
                    Color.Lerp(Ui.SuccessSurface, Ui.Good, .45f)), 136, 24);
                badge.raycastTarget = false;
                var bonus = Ui.Label(badge.transform, $"玩家在店 <size=17>+{ShopSimulation.OwnerOnSiteBonus:0%}</size>",
                    14, TextAnchor.MiddleCenter, Ui.Good);
                bonus.fontStyle = FontStyle.Bold;
                bonus.rectTransform.Fill(2);
                presence = badge.gameObject;
                var value = Ui.Label(card, "", 26, color: Ui.Accent);
                value.fontStyle = FontStyle.Bold;
                Ui.Size(value, -1, 27);
                note = Line(card, "", 13, Ui.Dim);
                Ui.Size(note, -1, 18);
                return value;
            }
        }

        // ---------- 库存 ----------

        private readonly HashSet<PlacedFurniture> _expandedStockShelves = new HashSet<PlacedFurniture>();

        private void BuildStock()
        {
            BuildRetailLinks(C);
            if(S.IsPetShop(S.CurrentShop) && S.City.HasPlace(PetShopCatalog.SupplierId))
                Ui.Size(Ui.Button(C,"前往暖爪宠物用品批发市场",()=>S.BeginCityTravel(PetShopCatalog.SupplierId),16),-1,34);
            ShopSimulation shop = S.PlayerShop;
            if(S.RetailKind=="restaurant") BuildRestaurantFridgeStock();
            Line(C, "货架陈列", 20).name = "StockShelvesHeading";
            _expandedStockShelves.RemoveWhere(p=>!S.ShopLayout.Contains(p));
            var shelfIdx = S.ShelfIndexes();
            _openingShelfCategoryButton = null;
            int tutorialIndex = S.OpeningShelfTutorialPaused ? S.OpeningShelfTutorialIndex() : -1;
            if (shelfIdx.Count == 0) Line(C, "尚未摆放货架。", 16, Ui.Dim);
            for (int si = 0; si < shelfIdx.Count; si++)
                BuildStockShelf(shop, shelfIdx[si], si + 1, shelfIdx[si] == tutorialIndex);

            // Stock is shared with the shelf planner. Split its live result from the
            // remainder, instead of displaying all Stock as if everything were on sale.
            var shown=new Dictionary<string,int>();
            var resaleOnShelf = new HashSet<int>();
            foreach(var kind in ShelfKinds.All)
            {
                var fill=shop.ShelfFillOf(kind);
                foreach(var line in fill.Parts)
                    shown["part:"+line.Part.Id+(line.Boxed?":b":":l")]=line.Count;
                foreach(var line in fill.Goods)
                    shown["stockgift:"+line.Key]=line.Value;
                foreach(var offer in fill.Resale) resaleOnShelf.Add(offer.Id);
            }
            if (shop.DisplayPcs.Count>0) Line(C,"展示台整机 · "+shop.DisplayPcs.Count+" 台",16,Ui.Dim);

            Line(C,"后仓 · 容量不限",19,Ui.Accent).name="StockWarehouseHeading";
            var warehouse=Ui.Rect(C,"StockWarehouseSection");Ui.VList(warehouse,2,8);
            if(!S.HasWarehouseHere){Line(warehouse,"未摆放纸箱。",16,Ui.Dim);return;}
            int rows=0;
            foreach(var item in CollectBox())
            {
                int count=item.Count-(shown.TryGetValue(item.Key,out int front)?front:0);
                if(count<=0)continue;
                rows++;
                bool byWeight = item.Key == "rice";
                var label = ReadOnlyStock(warehouse,item.Key,item.Name,count,"WarehouseItem_"+item.Key,byWeight ? item.Kg : (double?)null);
                if (!byWeight) label.Tip($"每件 {item.Kg/item.Count:0.###} 千克");
            }
            if (S.RetailKind=="secondhand")
                foreach (var item in S.SpecialBusiness(S.CurrentShop.Place.Id).Used.Where(i=>i.Owned&&!i.Sold&&!resaleOnShelf.Contains(i.Id)))
                {
                    rows++;
                    ReadOnlyStock(warehouse,GameSession.UsedItemPortrait(item),GameSession.UsedItemName(item)+" #"+item.Id,1);
                }
            if(rows==0)Line(warehouse,"后仓是空的。",16,Ui.Dim);
        }

        private void BuildStockShelf(ShopSimulation shop, int layoutIndex, int number, bool tutorialTarget)
        {
            var placed = S.ShopLayout[layoutIndex];
            var furniture = FurnitureCatalog.Get(placed.Id);
            var kind = S.ShelfKindAt(layoutIndex);
            var fill = shop.ShelfFillAt(layoutIndex, kind) ?? new ShelfFill(kind, furniture.ShelfKg, furniture.ShelfItems);
            bool expanded = _expandedStockShelves.Contains(placed);
            Color tanEdge = new Color32(193, 163, 120, 255);
            Color cardEdge = kind == null ? new Color32(181, 124, 29, 255) : tanEdge;
            var cardImage = Ui.FlatPanel(C, $"StockShelf_{layoutIndex}", new Color32(248, 236, 218, 255), cardEdge);
            if (kind == null) Ui.FlatBorder(cardImage, cardEdge, 1.5f);
            var card = cardImage.rectTransform;
            Ui.VList(card, 6, 8);

            var row = Ui.Row(card, 52, 8);
            row.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
            var identity = Ui.Size(Ui.FlatPanel(row, "ShelfIdentity", new Color32(252, 245, 230, 255), tanEdge), 106, 48);
            string tier = furniture.ShelfKg >= 160 ? "重型" : furniture.ShelfKg > ShelfKinds.CapacityKg ? "加固" : "普通";
            Ui.Label(identity.transform, $"<b>{number}号货架</b>\n<size=14><color=#655E52>{tier}</color></size>", 18, TextAnchor.MiddleCenter)
                .rectTransform.Fill(2);
            string category = kind == null ? "选择摆什么 ▾" : kind.Name + " ▾";
            var categoryButton = Ui.Size(Ui.Button(row, category, () => OpenShelfKindChooser(layoutIndex), 17,
                new Color32(248, 196, 95, 255)), 0, 38, 1).Tip(kind == null ? "设置陈列品类" : "更换陈列品类");
            Ui.FlatBorder(categoryButton.image, new Color32(148, 102, 24, 255));
            categoryButton.GetComponentInChildren<Text>().fontStyle = FontStyle.Bold;
            if (tutorialTarget) _openingShelfCategoryButton = categoryButton;

            var capacity = Ui.Size(Ui.Rect(row, "ShelfLoad"), 158, 46, 0);
            Ui.VList(capacity, 2, 0);
            string load = $"{fill.UsedKg:0.##}/{fill.CapacityKg:0.##} 千克\n{fill.Total}/{fill.CapacityItems} 件"
                + (fill.Full ? " · <color=#A42626><b>已满</b></color>" : "");
            Ui.Size(Ui.Label(capacity, load, 16, TextAnchor.MiddleLeft), -1, 34)
                .name = $"ShelfCapacity_{layoutIndex}";
            float weightLoad = fill.CapacityKg > 0 ? (float)(fill.UsedKg / fill.CapacityKg) : 0f;
            float itemLoad = fill.CapacityItems > 0 ? (float)fill.Total / fill.CapacityItems : 0f;
            Color meterInk = fill.Full ? new Color32(191, 74, 77, 255) : new Color32(132, 158, 93, 255);
            var meter = Ui.Bar(capacity, Mathf.Max(weightLoad, itemLoad), meterInk, 158, 8);
            var track = meter.transform.parent.GetComponent<Image>();
            track.color = new Color32(238, 227, 210, 255);
            Ui.Round(track);
            Ui.Round(meter);
            Ui.FlatBorder(track, new Color32(177, 158, 132, 255), .6f);

            var detailsButton = Ui.Size(Ui.Button(row, expanded ? "收起 ▴" : "商品 ▾", () =>
            {
                if (!_expandedStockShelves.Add(placed)) _expandedStockShelves.Remove(placed);
                RebuildDock();
            }, 16, expanded ? Ui.FlatRow : new Color32(244, 232, 208, 255)), 78, 36);
            Ui.FlatBorder(detailsButton.image, tanEdge);
            detailsButton.name = $"ToggleShelfItems_{layoutIndex}";
            if (!expanded) return;

            var details = Ui.FlatPanel(card, $"ShelfItems_{layoutIndex}", Ui.FlatWell, Ui.FlatCardEdge).rectTransform;
            Ui.VList(details, 2, 8);
            foreach (var line in fill.Parts)
                ReadOnlyStock(details, line.Part.Id, line.Part.Name + (line.Boxed ? " · 盒装" : " · 散件"), line.Count,
                    "ShelfPart_" + line.Part.Id + (line.Boxed ? "_b" : "_l"));
            foreach (var line in fill.Goods)
                ReadOnlyStock(details, line.Key, Gifts.Get(line.Key).Name, line.Value, "ShelfGoods_" + line.Key);
            foreach (var offer in fill.Resale)
                ReadOnlyStock(details, offer.Portrait ?? "used:" + offer.Product, offer.Name, 1);
            if (fill.Total == 0) Line(details, kind == null ? "先选择陈列品类。" : "这个货架是空的。", 15, Ui.Dim);
        }

        private static Text ReadOnlyStock(Transform parent,string itemKey,string name,int count,string rowName=null,double? totalKg=null)
        {
            var row = Ui.Row(parent,38,6);
            row.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
            if (rowName!=null) row.name = rowName;
            ItemPortrait(row,itemKey,34);
            var label = Ui.Size(Ui.Label(row,name,16,TextAnchor.MiddleLeft),0,-1,1);
            if (totalKg.HasValue)
                Ui.Size(Ui.Label(row,$"{totalKg.Value:0.##} 千克",16),90).name="StockQuantity";
            else StockCount(row,count);
            return label;
        }

        private static void StockCount(Transform parent,int count)=>Ui.Size(Ui.Label(parent,"×"+count,17,TextAnchor.MiddleLeft),64).name="StockQuantity";

        private void BuildRestaurantFridgeStock()
        {
            var fridge=S.CurrentShop.Fridge;
            Line(C, S.CanOpenFridge ? "冰箱库存 · " + FridgeLoadText(fridge.WeightKg, S.CurrentFridgeLimitKg)
                : "冰箱库存 · 尚未摆放", 19, Ui.Accent).name = "StockFridgeHeading";
            var section=Ui.Rect(C,"StockFridgeSection");Ui.VList(section,4,0);
            var items=CollectStacks(fridge,new Stock(0)).Where(item=>item.Count>0).ToArray();
            foreach(var item in items)
            {
                var row=Ui.Row(section,56,4);row.name="FridgeItem_"+item.Key;
                ItemPortrait(row,item.Key,46);
                Ui.Size(Ui.Label(row,item.Name+"\n<size=14>冰箱 · "+item.Kg.ToString("0.##")+" 千克 · 永久保存</size>",16),-1,-1,1);
                if(item.Key=="rice")Ui.Size(Ui.Label(row,item.Kg.ToString("0.##")+" 千克",16),90).name="StockQuantity";
                else StockCount(row,item.Count);
            }
            if (items.Length == 0 && S.CanOpenFridge) Line(section, "冰箱是空的。", 16, Ui.Dim);
            Ui.Size(Ui.Button(section, "管理食材", OpenFridgeWindow, 16, Ui.ButtonBg), -1, 34);
        }

        // ---------- 定价 ----------

        private sealed class PricingRow
        {
            public RectTransform Root;
            public int Count;
            public System.Func<double, double> Refresh;
        }

        private ShopSimulation _pricingShop;
        private System.Action _refreshPricing;

        private void BuildPricing()
        {
            if(S.IsSpecialBusiness(S.CurrentShop))
            {
                BuildSpecialPricing();
                return;
            }
            var shop=S.PlayerShop;
            _pricingShop = shop;
            Line(C,RetailShopTypes.Name(S.RetailKind)+" · 仅列货架上已陈列的商品",17,Ui.Accent);
            Line(C,"可逐件调价，点“确定”生效；未单独定价的商品跟随统一加价。",15,Ui.Dim);
            var global=Line(C,"",19,Ui.Good);
            global.name="PricingPurchaseIntentSummary";
            global.Tip("按陈列件数汇总愿意且买得起的比例。单品估算针对正在考虑这款商品的顾客。");
            var rows = new Dictionary<string, PricingRow>();
            double draftMarkup = shop.Markup;
            void RefreshAll(double markup)
            {
                draftMarkup = markup;
                double total = rows.Values.Sum(row => row.Refresh(markup));
                long displayedCount = rows.Values.Sum(row => (long)row.Count);
                string label = System.Math.Abs(markup - shop.Markup) > .001 ? "预览购买意愿" : "参考购买意愿";
                global.text = displayedCount > 0 ? $"{label} {PurchaseEstimateText(total / displayedCount)}" : "购买意愿：暂无商品";
            }
            var editor = Ui.Rect(C, "PricingMarkupEditor");
            Ui.VList(editor, 4, 0);
            var list=Ui.Rect(C,"PricingProducts");Ui.VList(list,4,0);
            var empty = Line(list, "货架上暂无商品，陈列后可在这里调价。", 17, Ui.Dim);
            PricingRow CreateRow(string key,string itemId,string name,double kg,System.Func<double, Money> price,System.Func<Money, double> intent,
                System.Action edit,System.Func<bool> own,System.Action follow)
            {
                var view = new PricingRow { Root = Ui.Row(list, 86, 6) };
                view.Root.name="Pricing_"+key;
                ItemPortrait(view.Root,itemId,52);
                var caption = Ui.Size(ProductText(view.Root, "", 16), -1, -1, 1);
                var current=Ui.Size(Ui.Label(view.Root,"",16,TextAnchor.MiddleLeft,Ui.Good),104);
                var editButton=Ui.Size(Ui.Button(view.Root,"调价",edit,16,Ui.ButtonBg),62,38);editButton.name="EditPrice_"+key;
                var followButton=Ui.Size(Ui.Button(view.Root,"跟随",()=>{follow();RefreshAll(draftMarkup);},15),56,38);
                view.Refresh = markup =>
                {
                    caption.text = ProductLabel(name, kg, $"已陈列 {view.Count} 件").Replace("<size=13>", "<size=14>");
                    Money shownPrice = price(markup);
                    double chance = intent(shownPrice);
                    current.text = shownPrice + $"\n<size=14>购买意愿 {PurchaseEstimateText(chance)}</size>";
                    followButton.interactable = own();
                    return view.Count * chance;
                };
                return view;
            }

            void RefreshProducts()
            {
                var fills = ShelfKinds.All.Select(shop.ShelfFillOf).ToArray();
                int index = 0;
                var visible = new HashSet<string>();
                void Show(string key, PricingRow row, int count)
                {
                    visible.Add(key);
                    row.Count = count;
                    if (row.Root.GetSiblingIndex() != index) row.Root.SetSiblingIndex(index);
                    index++;
                }

                foreach (var item in fills.SelectMany(fill => fill.Goods).Where(item => item.Value > 0))
                {
                    string id = item.Key, key = "goods_" + id;
                    if (!rows.TryGetValue(key, out var row))
                    {
                        var product = Gifts.Get(id);
                        rows[key] = row = CreateRow(key, id, product.Name, product.WeightKg, markup => shop.GoodsPriceOf(id, markup),
                            price => shop.GoodsPurchaseEstimateAtPrice(id, price), () => OpenWindow("调价 · " + product.Name, 600, 300, w =>
                            {
                                Para(w.Content, product.Name + " · 调整时预览购买意愿，确定后保存售价。", 17);
                                Ui.Size(ShelfSaleTag(w, w.Content, key, shop.GoodsPriceOf(id).Yuan, shop.GoodsPriceRange(id),
                                    value => shop.GoodsPurchaseEstimateAtPrice(id, value), value => { S.ConfirmGoodsSalePrice(id, value); RefreshAll(draftMarkup); }), -1, 100);
                            }), () => shop.HasGoodsMarkup(id), () => shop.SetGoodsMarkup(id, null));
                    }
                    Show(key, row, item.Value);
                }
                foreach (var item in fills.SelectMany(fill => fill.Parts).Where(item => item.Count > 0))
                {
                    var part = item.Part;
                    bool boxed = item.Boxed;
                    string key = part.Id + (boxed ? "_boxed" : "_loose");
                    if (!rows.TryGetValue(key, out var row))
                    {
                        rows[key] = row = CreateRow(key, part.Id, part.Name + (boxed ? " · 盒装" : " · 散件"), PartUnitKg(part, boxed),
                            markup => shop.PriceOf(part, boxed, markup), price => shop.PartPurchaseIntentAtPrice(part, boxed, price.Yuan),
                            () => OpenWindow("调价 · " + part.Name, 600, 300, w =>
                            {
                                Para(w.Content, part.Name + " · " + (boxed ? "盒装" : "散件"), 17);
                                Ui.Size(ShelfSaleTag(w, w.Content, key, shop.PriceOf(part, boxed).Yuan, shop.PartPriceRange(part, boxed),
                                    value => shop.PartPurchaseIntentAtPrice(part, boxed, value), value => { S.ConfirmPartSalePrice(part, boxed, value); RefreshAll(draftMarkup); }), -1, 100);
                            }), () => shop.HasPartMarkup(part), () => shop.SetPartMarkup(part, null));
                    }
                    Show(key, row, item.Count);
                }
                foreach (string key in rows.Keys.Where(key => !visible.Contains(key)).ToArray())
                {
                    var removed = rows[key].Root.gameObject;
                    removed.SetActive(false);
                    Kill.It(removed);
                    rows.Remove(key);
                }
                empty.gameObject.SetActive(rows.Count == 0);
            }

            RefreshProducts();
            var syncEditor = PriceMultiplierEditor(editor, "GlobalMarkupSlider", "统一加价", () => shop.Markup,
                ShopSettings.MinMarkup, ShopSettings.MaxMarkup, .05, S.SetMarkup,
                draftKey: "global:" + S.CurrentShop.Place.Id, onPreview: RefreshAll);
            _refreshPricing = () =>
            {
                RefreshProducts();
                syncEditor();
            };
        }

        // ---------- 经营报表 ----------

        /// <summary>近 14 天每天的营业额（绿）和支出（红）柱状图。</summary>
        public void OpenReportWindow() => OpenFinanceTrend(S.CurrentShop?.Rented == true ? S.CurrentShop.Place.Id : null);
        private void OpenFinanceTrend(string shopId)
        {
            string scope = shopId == null ? "全部门店" : S.ShopTitle(S.ShopAt(shopId));
            OpenWorkspace("finance-trend", shopId ?? "all", "经营趋势 · " + scope, 960, 680, w =>
            {
                var days = FinanceReport.OperatingDaily(S.Ledger, S.Clock.Today, 14, shopId, S.ShopAt(shopId)?.SalesAccountId);
                Money rev = Money.Zero, exp = Money.Zero, other = Money.Zero;
                long max = 100;
                foreach (var d in days)
                {
                    rev += d.Revenue;
                    exp += d.Expense;
                    other += d.OtherIncome;
                    max = System.Math.Max(max, System.Math.Max(d.Revenue.Cents, d.Expense.Cents));
                }
                Line(w.Content, $"近 {days.Count} 天：销售 {rev}　经营支出 {exp}　经营现金净额 {rev + other - exp}", 19);
                Line(w.Content, "<color=#386047>■</color> 销售收款　<color=#9C4634>■</color> 经营支出", 15, Ui.Dim);

                Line(w.Content, $"{scope} · 其他经营收款 {other}；个人消费、借款和押金不计入本图。", 15, Ui.Dim);
                Image plot = Ui.Panel(w.Content, "Chart", Ui.PanelBg2);
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
                    Ui.Panel(area, "Rev", Ui.Good).rectTransform.Anchor(0.05f, 0, 0.48f, hr);
                    Ui.Panel(area, "Exp", Ui.Warn).rectTransform.Anchor(0.52f, 0, 0.95f, he);
                    Text lbl = Ui.Label(col, $"{d.Date.Month}/{d.Date.Day}\n<size=11><color=#386047>{(d.Revenue.IsZero ? "-" : "¥" + d.Revenue.Yuan)}</color></size>\n<size=11><color=#9C4634>{(d.Expense.IsZero ? "-" : "¥" + d.Expense.Yuan)}</color></size>", 12, TextAnchor.UpperCenter);
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
            Ui.Size(Ui.Button(C,"企业采购订单",()=>OpenBusinessSection(2),17),-1,35).name="ShopEnterpriseOrders";
            ShopSimulation shop = S.PlayerShop;
            if (shop.IsGoodsShop) { Line(C, "这家店经营零售商品，不接电脑装机订单。", 18, Ui.Dim); return; }
            if (!shop.BuildOrdersUnlocked) Line(C, "开局前 14 天不接顾客装机订单，第 15 天开始开放。", 17, Ui.Accent);
            Line(C, $"散客 {shop.CustomerBuildOrders}/{ShopSimulation.CustomerBuildOrderLimit} 单 · 待交付 {shop.Orders.Count}/{ShopSimulation.BuildOrderLimit} 台", 17, Ui.Accent);
            Line(C, $"散客订单 {shop.Settings.BuildOrderDueDays} 天内交付。接单后自动预留配件，缺件到货即补。", 15, Ui.Dim);
            _buildOrdersSignature = BuildOrdersSignature();
            Transform list = C;
            foreach (BuildOrder o in shop.Orders)
            {
                BuildOrder order = o;
                List<PartDef> missing = S.MissingParts(o);
                Text head = CounterText(() => $"<b>{PartCatalog.TierName(order.Tier)}整机</b>  报价 <color=#386047>{order.Price}</color>  {S.Clock.OrderDeadlineText(order.DueAt.TotalMinutes)}", 18);
                Ui.Size(head, -1, 28);
                foreach (PartDef p in PartCatalog.BuildList(o.Tier))
                {
                    string status = order.HasReservedPart(p) ? "<color=#386047>已预留</color>" : "<color=#9C4634>缺货</color>";
                    Ui.Size(Ui.Label(list, $"   {PartCatalog.CategoryName(p.Category)}：{p.Name} · {status}", 16), -1, 22);
                }
                string action = missing.Count > 0 ? $"还缺 {missing.Count} 样配件" :
                    order.EmployeeMinutesLeft > 0 ? "店员装机中" : "开始装机";
                Ui.Size(Ui.Button(list, action, () => OpenBuildMinigame(order), 18, Ui.ButtonHot, missing.Count == 0 && order.EmployeeMinutesLeft == 0), -1, 40);
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
            Para(C, $"现在是{GameSession.SizeName(S.PlayerShop.Size)}，周租 {S.CurrentShopRent}。想保留库存的话，可以先叫货车搬走。退租时会清空留下的货物和装修。", 15, Ui.Accent);
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
                string status = owned ? "<color=#386047>已装好</color>"
                    : $"{u.Price}　去<color=#80551F>{u.SoldAtName}</color>买" + (blocker != null && blocker != "钱不够" ? $"　<color=#9C4634>{blocker}</color>" : "");
                Para(list, $"<b>{u.Name}</b>　{status}\n<size=14>{u.Description}</size>", 17);
            }
            Para(list, "智能手机、收款码、自助结账、监控在街上的手机店；招牌灯箱、空调和更大更结实的货架在宜居家具城。自助结账一年目标达标也会赠送。", 14, Ui.Dim);
        }

        // ---------- 招人 ----------

        private string _staffStructure;
        private readonly List<System.Action> _staffRefresh = new List<System.Action>();

        private string StaffStructure(EmployeeAssignment[] roster)
        {
            var shop = S.CurrentShop;
            return $"{shop.Place.Id}|{shop.RetailKind}|{S.ShopStaffLimit}|{S.NextCandidateArrivalText}|"
                + $"{S.OpeningBuyerPlanReady(shop)}|{S.OpeningStaffStep}|"
                + string.Join(",", S.Candidates.Select(c => c.Id)) + "|"
                + string.Join(";", roster.Select(r =>
                    $"{r.Person.Id}:{r.Role}:{r.Shop?.Place.Id}:"
                    + (r.Shop == shop && r.Role == EmployeeRole.Buyer && BuyerNeedsPurchasePlan(r.Person))));
        }

        private void BuildStaff()
        {
            var roster = S.EmployeesForShop(S.CurrentShop);
            _staffStructure = StaffStructure(roster);
            _openingStaffCandidates = null;
            _openingStaffFirstApplicant = null;
            _openingStaffRecruitmentNotice = null;
            _openingStaffPurchaseButton = null;
            Line(C,GameSession.SizeName(S.PlayerShop.Size)+" · 最多 "+S.ShopStaffLimit+" 名员工 · 已占用 "+S.StaffCommitted(S.CurrentShop)+" 个名额",18,Ui.Accent).name="RecruitmentShopCapacity";
            bool guidedHire = S.OpeningStaffStep == OpeningStaffLesson.Hire;
            if (guidedHire)
            {
                _openingStaffCandidates = Ui.Panel(C, "OpeningStaffCandidates", Ui.PanelBg2).rectTransform;
                Ui.VList(_openingStaffCandidates, 4, 8);
                BuildRecruitment(_openingStaffCandidates, guided: true);
            }
            BuildOpeningStaffChecklist(S.CurrentShop);
            Line(C,"本店员工",19,Ui.Accent);
            if(roster.Length==0)Line(C,"本店还没有员工。",17,Ui.Dim);
            foreach(var assignment in roster)
            {
                var worker=assignment.Person;string personId=S.PersonIdOf(worker);
                bool isShopBuyer = assignment.Shop == S.CurrentShop && assignment.Role == EmployeeRole.Buyer;
                bool needsPlan = isShopBuyer && BuyerNeedsPurchasePlan(worker);
                var row=Ui.Row(C,108,8);row.name="ShopEmployee_"+worker.Id;
                PortraitCard(row,personId,worker.Name,"",64,()=>OpenPersonDialogue(personId),tagText:null);
                var card = Ui.Size(Ui.Button(row, "", () => OpenPersonDialogue(personId), 16, Ui.ButtonBg), -1, -1, 1);
                card.name="ShopEmployeeChat_"+worker.Id;
                var label = card.GetComponentInChildren<Text>();
                void RefreshEmployeeCard()
                {
                    bool incoming = assignment.Shop != S.CurrentShop && worker.TransferShopId == S.CurrentShop.Place.Id;
                    string role = GameSession.RoleName(incoming ? S.PlannedEmployeeRole(assignment) : assignment.Role);
                    string state = incoming ? "明日调入 · 当前" + (assignment.Shop == null ? "待安排" : S.ShopTitle(assignment.Shop))
                        : ShopEmployeeWorkText(assignment);
                    if (needsPlan)
                    {
                        var plan = worker.PurchasePlan;
                        bool paused = plan?.InventoryThreshold == true || (plan?.Items?.Length ?? 0) > 0;
                        string reminder = paused ? "采购已暂停" : "未安排进货";
                        state += $" · <b><color=#{ColorUtility.ToHtmlStringRGB(Ui.FlatDown)}>{reminder}</color></b>";
                    }
                    var nextShop = S.ShopAt(worker.TransferShopId);
                    string next = nextShop != null && !incoming ? "\n明日调往：" + S.ShopTitle(nextShop) : "";
                    string text = $"<b>{worker.Name}</b> · {role}\n{state}\n班次 {Ui.Hm(worker.ShiftStart)}—{Ui.Hm(worker.ShiftEnd)} · 日薪 {worker.DailyWage}{next}";
                    if (label.text != text) label.text = text;
                }
                RefreshEmployeeCard();
                _staffRefresh.Add(RefreshEmployeeCard);
                if (needsPlan) AddPurchaseAlertDot(card.transform);
                if (isShopBuyer)
                {
                    var purchase = Ui.Size(Ui.Button(row, "安排进货", () => OpenEmployeePurchase(worker.Id), 15,
                        needsPlan ? Ui.ButtonHot : Ui.ButtonBg), 96, 36);
                    if (_openingStaffPurchaseButton == null) _openingStaffPurchaseButton = purchase;
                }
            }
            Ui.Size(Ui.Button(C,"管理岗位与排班",OpenEmployeesPanel,17),-1,38);
            if (!guidedHire) BuildRecruitment(C);
        }

        private void BuildRecruitment(Transform parent, bool guided = false)
        {
            var shop = S.CurrentShop;
            var missingRoles = guided ? S.MissingOpeningStaffRoles(shop).ToArray() : System.Array.Empty<EmployeeRole>();
            Line(parent, "招聘", 19, Ui.Accent);
            if (!guided) _openingStaffRecruitmentNotice = BuildRecruitmentNotice(parent, RebuildDock);
            var candidates = S.Candidates.Where(S.CandidateAvailable).ToArray();
            if (candidates.Length == 0)
            {
                if (!guided && !S.RecruitmentNoticePosted) Line(parent, S.RecruitmentHoursText, 15, Ui.Dim);
                return;
            }
            Line(parent, $"今日应聘者 · 剩 {candidates.Length} 位 · 18:00 离开", 17);
            Button previous = null;
            foreach (var candidate in candidates)
            {
                string personId = S.PersonIdOf(candidate);
                string abilities = $"智力 {candidate.Intelligence} · 魅力 {GameSession.EmployeeCharm(candidate)}\n动手能力 {candidate.Dexterity} · 体能 {candidate.Stamina}";
                System.Action talk = () => OpenPersonDialogue(personId);
                var row = PersonSummary(parent, personId, $"{candidate.Name} · 日薪 {candidate.DailyWage}", abilities, 68,
                    onClick: guided ? null : talk);
                bool available = !guided || missingRoles.Any(role => S.RecruitAssignmentBlocker(candidate.Id, shop.Place.Id, role) == null);
                var hire = Ui.Size(Ui.Button(row, "聘用并安排工作", () => OpenRecruitWork(candidate, guided),
                    16, Ui.ButtonHot, available), 132, 36);
                Ui.Size(Ui.Button(row, "交谈", talk, 16, Ui.ButtonBg, !guided), 66, 36);
                if (!guided || !available) continue;
                if (_openingStaffFirstApplicant == null) _openingStaffFirstApplicant = hire;
                hire.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = previous };
                if (previous != null)
                {
                    var navigation = previous.navigation;
                    navigation.selectOnDown = hire;
                    previous.navigation = navigation;
                }
                previous = hire;
            }
        }

        private Button BuildRecruitmentNotice(Transform parent, System.Action refresh)
        {
            string label = S.RecruitmentNoticePosted ? $"招聘告示已贴 · {S.NextCandidateArrivalText} 到店" : "贴招聘告示";
            var button = Ui.Size(Ui.Button(parent, label, () =>
            {
                if (S.PostRecruitmentNotice()) refresh();
            }, 17, Ui.ButtonHot, S.CanPostRecruitmentNotice), -1, 44);
            button.name = "PostRecruitmentNotice";
            return button;
        }

        private IEnumerable<(EmployeeRole role, bool required)> OpeningStaffOverviewRoles(OwnedShop shop)
        {
            foreach (var role in S.OpeningStaffRoles(shop)) yield return (role, true);
            if (shop.RetailKind == "secondhand") yield return (EmployeeRole.Repairer, false);
        }

        private void BuildOpeningStaffChecklist(OwnedShop shop)
        {
            _openingStaffDockControl = null;
            var panel = Ui.Panel(C, "OpeningStaffChecklist", Ui.PanelBg2).rectTransform;
            Ui.VList(panel, 4, 10);
            Line(panel, "开店分工", 19, Ui.Accent);
            var staffing = S.OpeningStaffStep;
            if (staffing == OpeningStaffLesson.Roles)
                _openingStaffDockControl = Ui.Size(Ui.Button(panel, "查看本店岗位", () => OpenOpeningStaffLesson(shop), 17, Ui.ButtonHot), -1, 40);
            foreach (var (role, required) in OpeningStaffOverviewRoles(shop))
            {
                bool assigned = S.ShopStaff(shop, role).Any();
                bool ready = assigned && (role != EmployeeRole.Buyer || S.OpeningBuyerPlanReady(shop));
                string status = ready ? "已安排" : assigned ? "待设采购" : required ? "待安排" : "未安排";
                var row = Ui.Row(panel, 26, 6);
                string title = S.OpeningStaffRoleName(shop, role) + (required ? "" : " · 选配");
                Ui.Size(Ui.Label(row, title, 16, TextAnchor.MiddleLeft, Ui.Accent), 108);
                Ui.Size(Ui.Label(row, S.OpeningStaffRoleDuty(shop, role), 15, TextAnchor.MiddleLeft, Ui.Dim), 0, -1, 1);
                Ui.Size(Ui.Label(row, status, 15, TextAnchor.MiddleRight, ready ? Ui.Good : required ? Ui.Warn : Ui.Dim), 90);
            }
            if (S.OpeningStaffSpecialty(shop) is string specialty) Para(panel, specialty, 15, Ui.Dim);
            if (staffing != OpeningStaffLesson.Hire && S.OpeningStaffNextStep(shop) is string next) Para(panel, next, 15, Ui.Accent);
        }

        private void OpenOpeningStaffLesson(OwnedShop shop)
        {
            _openingStaffLessonWindow = OpenWindow("开店教学 · 员工分工", 820, 370, w =>
            {
                Line(w.Content, S.ShopTitle(shop), 22, Ui.Accent);
                foreach (var (role, required) in OpeningStaffOverviewRoles(shop))
                {
                    string title = S.OpeningStaffRoleName(shop, role) + (required ? "" : " · 选配");
                    Para(w.Content, $"<b>{title}</b>　{S.OpeningStaffRoleDuty(shop, role)}", 20, required ? Ui.TextColor : Ui.Dim);
                }
                if (S.OpeningStaffSpecialty(shop) is string specialty) Para(w.Content, specialty, 17, Ui.Dim);
                _openingStaffLessonDone = Ui.Size(Ui.Button(w.Content, "已了解分工", () =>
                {
                    if (!S.FinishOpeningStaffRoles()) return;
                    w.Close();
                    RebuildDock();
                }, 19, Ui.ButtonHot), -1, 46);
            }, fitContent: true);
        }

        private string ShopEmployeeWorkText(EmployeeAssignment row)
        {
            if(row.Role==EmployeeRole.Unassigned||!S.CityEmployeeAvailable(row.Person,row.Shop))return S.EmployeeStatus(row);
            switch(row.Role)
            {
                case EmployeeRole.Cashier:
                    int waiting=row.Shop.Sim.Queue.Count;
                    return waiting>0?"在岗 · 接待结账（"+waiting+" 人排队）":"在岗 · 收银值守，等待顾客";
                case EmployeeRole.Assembler:return !row.Shop.AutoBuild?"在岗 · 自动装机未开启":row.Shop.Sim.Orders.Count>0?"在岗 · 装机值守（店内 "+row.Shop.Sim.Orders.Count+" 单待处理）":"在岗 · 等待装机订单";
                case EmployeeRole.Salesperson:return "在岗 · 导购与销售接待";
                case EmployeeRole.Buyer:
                    var trip=S.EmployeePurchaseTrips.FirstOrDefault(t=>t.EmployeeId==row.Person.Id);
                    return trip!=null?S.EmployeePurchaseTripStatus(trip):row.Person.PurchasePlan?.Daily==true?"在店待命 · 等待下次采购":"在店待命";
                default:return S.EmployeeStatus(row);
            }
        }
    }
}
