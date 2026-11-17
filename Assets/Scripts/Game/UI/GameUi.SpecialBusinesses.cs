using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Home;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using UnityEngine;
using UnityEngine.UI;

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
            BuildHardwareMarketNews(w);
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

            if (_tab == ShopTab.Counter)
                return $"{RestaurantCounterSlots(state)}:{S.CanOpenFridge}";

            string orders = string.Join(",", state.Orders.Select(o => $"{o.Id}:{o.Table}:{o.Recipe}:{o.FishId}:{o.Price}:{o.Stage}:{o.TableCleared}"));

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
                Para(w.Content, "大米存后仓，其他食材存冰箱，备料时自动取用。灶台下锅后，8分钟熟，38分钟焦糊。出锅后直接上菜。服务员可上菜、收桌并收款，你也可以到餐桌旁按 F 或在柜台面板收桌。店里需要摆放收银台才能收款。", 18));

        private void BuildRestaurantCounter(SpecialBusinessState state)
        {
            var shop=S.CurrentShop;
            if (S.CanOpenFridge)
            {
                var storage = Ui.Row(C, 40, 6);
                var storageLayout = storage.GetComponent<HorizontalLayoutGroup>();
                storageLayout.padding = new RectOffset(9, 5, 3, 3);
                var surface = storage.gameObject.AddComponent<Image>();
                surface.color = Ui.PanelBg2;
                surface.raycastTarget = false;
                Ui.Round(surface);
                var fill = Ui.Size(Ui.Label(storage, "", 15, color: Ui.Dim), -1, 34, 1);
                void RefreshFridge() => fill.text = $"冰箱 {shop.Fridge.WeightKg:0.##} / {S.CurrentFridgeLimitKg:0.##} 千克";
                RefreshFridge();
                _counterRefresh.Add(RefreshFridge);
                Ui.Size(Ui.Button(storage, "打开冰箱", OpenFridgeWindow, 15), 96, 34);
            }

            var drinkShelf = ShelfKinds.Get("drink");
            var drinks = Line(C, "", 15, Ui.Dim);
            void RefreshDrinks()
            {
                bool stocked = shop.Sim.ShelfFillOf(drinkShelf).Goods.Any(line => line.Value > 0 && RetailProducts.IsDrink(line.Key));
                drinks.text = stocked ? "饮料已上架 · 堂食顾客可加购" : "上架饮料后，堂食顾客可加购，有机会多赚一笔。";
                drinks.color = stocked ? Ui.Dim : Ui.Accent;
            }
            RefreshDrinks();
            _counterRefresh.Add(RefreshDrinks);

            var fish = Ui.Size(Ui.Button(C, "", () => SelectTab(ShopTab.Orders), 16), -1, 36).GetComponentInChildren<Text>();
            var tablesHeader = Ui.Row(C, 25);
            var label = Ui.Size(Ui.Label(tablesHeader, "餐桌", 17), 0, 25, 1);
            label.fontStyle = FontStyle.Bold;
            var tableSummary = Ui.Size(Ui.Label(tablesHeader, "", 14, TextAnchor.MiddleRight, Ui.Dim), 160, 25);
            int tableCount = RestaurantCounterSlots(state);
            var orders = new DiningOrder[tableCount];
            var workers = new Dictionary<int, RestaurantWorker>();
            var refreshTables = new Action<DiningOrder, RestaurantWorker, bool, long>[tableCount];
            for (int table = 0; table < tableCount; table++)
                refreshTables[table] = BuildRestaurantTableSlot(state, table, RefreshTables);
            if (tableCount == 0) Line(C, "暂无餐桌", 16, Ui.Dim);
            RefreshTables();
            _counterRefresh.Add(RefreshTables);

            void RefreshTables()
            {
                Array.Clear(orders, 0, orders.Length);
                int fishOrders = 0;
                foreach (var order in state.Orders)
                {
                    if (order.IsFishOrder) fishOrders++;
                    else if (!order.TableCleared && order.Table >= 0 && order.Table < orders.Length)
                        orders[order.Table] ??= order;
                }
                fish.text = $"查看鱼餐预订 · {fishOrders} 单";
                tableSummary.text = $"{orders.Count(order => order != null)} / {tableCount} 桌有订单";
                workers.Clear();
                bool playerBusy = false;
                foreach (var worker in state.Workers)
                {
                    if (!worker.Busy) continue;
                    playerBusy |= worker.ActorId == GameSession.RestaurantPlayer;
                    if (!workers.ContainsKey(worker.OrderId)) workers.Add(worker.OrderId, worker);
                }
                long now = S.Clock.Now.TotalMinutes;
                for (int table = 0; table < orders.Length; table++)
                {
                    var order = orders[table];
                    RestaurantWorker assigned = null;
                    if (order != null) workers.TryGetValue(order.Id, out assigned);
                    refreshTables[table](order, assigned, playerBusy, now);
                }
            }
        }

        private int RestaurantCounterSlots(SpecialBusinessState state)
        {
            var shop = S.CurrentShop;
            int limit = GameSession.RestaurantTableLimit(shop.Sim.Size);
            int count = Math.Min(limit, GameSession.RestaurantTablesPlaced(shop));
            // Retain table orders that still need clearing after a layout change.
            foreach (var order in state.Orders)
                if (!order.IsFishOrder && !order.TableCleared && order.Table >= count && order.Table < limit)
                    count = order.Table + 1;
            return count;
        }

        private Action<DiningOrder, RestaurantWorker, bool, long> BuildRestaurantTableSlot(
            SpecialBusinessState state, int table, Action refresh)
        {
            // Keep each physical table's space reserved, including when it has no guest or action.
            var slot = Ui.Row(C, 70, 8);
            slot.name = "RestaurantTable_" + table;
            var layout = slot.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 5, 5);
            layout.childForceExpandHeight = false;
            var surface = slot.gameObject.AddComponent<Image>();
            surface.color = Ui.PanelBg2;
            surface.raycastTarget = false;
            Ui.Round(surface);
            Ui.FlatBorder(surface, Ui.Edge);

            var icon = Ui.Size(Ui.Rect(slot, "DishPortrait"), 44, 44);
            var details = Ui.Size(Ui.Rect(slot, "TableDetails"), 0, 52, 1);
            Ui.VList(details, 1, 0);
            var title = Ui.Size(Ui.Label(details, "", 17), -1, 25);
            title.fontStyle = FontStyle.Bold;
            title.verticalOverflow = VerticalWrapMode.Truncate;
            var status = Ui.Size(Ui.Label(details, "", 14), -1, 24);
            status.resizeTextForBestFit = true;
            status.resizeTextMinSize = 12;
            status.resizeTextMaxSize = 14;
            status.verticalOverflow = VerticalWrapMode.Truncate;
            var bar = Ui.Size(Ui.Rect(slot, "TableAction"), 134, 38);
            string tableName = $"{table + 1}号桌";
            int recipe = -1;
            DiningOrder displayed = null;
            RestaurantJob job = RestaurantJob.None;
            bool cancel = false;
            var action = Ui.Button(bar, "", () =>
            {
                if (displayed == null) return;
                if (cancel)
                {
                    if (state.Workers.Any(w => w.Busy && w.ActorId == GameSession.RestaurantPlayer && w.OrderId == displayed.Id))
                        S.CancelPlayerRestaurantWork();
                }
                else if (job != RestaurantJob.None) _ctrl.RequestRestaurantWork(displayed.Id, job);
                refresh();
            }, 15, Ui.ButtonHot);
            action.image.rectTransform.Fill();
            var caption = action.GetComponentInChildren<Text>();

            return (order, assigned, playerBusy, now) =>
            {
                displayed = order;
                int currentRecipe = order?.Recipe ?? -1;
                if (currentRecipe != recipe)
                {
                    recipe = currentRecipe;
                    Ui.Clear(icon);
                    if (recipe >= 0) FillItemPortrait(icon, "recipe:" + recipe).Fill();
                }
                title.text = order == null ? tableName : $"{tableName} · {order.DishName} · ¥{order.Price}";
                title.color = order == null ? Ui.Dim : Ui.TextColor;
                status.text = RestaurantTableStatus(order, assigned, now);
                status.color = order == null || order.Stage == DiningStage.Eating ? Ui.Dim
                    : order.Stage == DiningStage.Burnt ? Ui.Warn
                    : order.Stage == DiningStage.Waiting ? Ui.Accent : Ui.Good;
                cancel = assigned?.ActorId == GameSession.RestaurantPlayer;
                var (nextJob, label) = order?.Stage switch
                {
                    DiningStage.Waiting => (RestaurantJob.Cook, "备料投锅"),
                    DiningStage.Cooking => (RestaurantJob.Serve, "出锅上菜"),
                    DiningStage.Ready => (RestaurantJob.Serve, "上菜"),
                    DiningStage.Burnt => (RestaurantJob.Clear, "倒掉重做"),
                    DiningStage.Checkout => (RestaurantJob.Clear, "收桌并收款"),
                    DiningStage.Completed or DiningStage.Left => (RestaurantJob.Clear, "收桌"),
                    _ => (RestaurantJob.None, "")
                };
                job = nextJob;
                caption.text = cancel ? "取消本次操作" : label;
                bool ready = order?.Stage != DiningStage.Cooking || (now >= order.ReadyAt && now < order.BurnAt);
                bool available = cancel || (job != RestaurantJob.None && assigned == null && !playerBusy && ready);
                Ui.SetButtonFill(action, cancel ? Ui.Warn : available ? Ui.ButtonHot : Ui.ButtonBg);
                action.interactable = available;
                action.gameObject.SetActive(cancel || job != RestaurantJob.None);
            };
        }

        private string RestaurantTableStatus(DiningOrder order, RestaurantWorker assigned, long now)
        {
            if (order == null) return "空桌";
            if (assigned != null)
            {
                string who = assigned.ActorId == GameSession.RestaurantPlayer ? "你" : S.FindEmployee(assigned.ActorId)?.Person.Name ?? "员工";
                if (assigned.Carrying) return who + " · 正在送往餐桌";
                string target = FurnitureCatalog.Get(assigned.Station)?.Name ?? "操作位置";
                return who + " · " + (assigned.OperateUntil < 0 ? "前往" + target : "正在" + GameSession.RestaurantJobName(assigned.Job));
            }
            return order.Stage switch
            {
                DiningStage.Waiting => "等待烹饪",
                DiningStage.Cooking => now < order.ReadyAt ? $"烹饪中 · 还有{order.ReadyAt - now}分钟熟"
                    : $"可出锅 · 距焦糊{Math.Max(0, order.BurnAt - now)}分钟",
                DiningStage.Ready => "已出锅 · 待上菜",
                DiningStage.Eating => "正在用餐",
                DiningStage.Checkout => "用餐完毕 · 待收桌收款",
                DiningStage.Completed => "已付款 · 待收桌",
                DiningStage.Burnt => "焦糊，需重做",
                _ => "顾客已离开"
            };
        }
        private void BuildSpecialPricing()
        {
            var state=S.SpecialBusiness(S.CurrentShop.Place.Id);
            if(S.RetailKind=="secondhand") { BuildUsedStock(C,pricingOnly:true); return; }
            _pricingShop = S.PlayerShop;
            Line(C,"菜单与饮料定价",20,Ui.Accent);
            var recipes = new Button[GameSession.RecipeNames.Length];
            for (int recipe = 0; recipe < recipes.Length; recipe++)
            {
                int selected = recipe;
                recipes[recipe] = Ui.Size(Ui.Button(C, "", () =>
                {
                    state.RecipeMask ^= 1 << selected;
                    RefreshRecipes();
                }, 16), -1, 40);
                ItemButtonPortrait(recipes[recipe], "recipe:" + recipe, 32);
            }
            void RefreshRecipes()
            {
                for (int recipe = 0; recipe < recipes.Length; recipe++)
                {
                    bool enabled = (state.RecipeMask & (1 << recipe)) != 0;
                    var button = recipes[recipe];
                    button.GetComponentInChildren<Text>().text = (enabled ? "✓ 供应 " : "暂停 ") + GameSession.RecipeNames[recipe]
                        + " · ¥" + Math.Round(GameSession.RecipePrices[recipe] * state.MenuMarkup);
                    Ui.SetButtonFill(button, Ui.SelectedButton(enabled));
                }
            }

            var refreshMenu = BuildMenuMarkupEditor(state, RefreshRecipes);
            var refreshDrinks = BuildRestaurantDrinkPricing();
            _refreshPricing = () => { RefreshRecipes(); refreshMenu(); refreshDrinks(); };
            _refreshPricing();
        }

        private Action BuildMenuMarkupEditor(SpecialBusinessState state, Action refreshRecipes)
        {
            string key = "menu:" + S.CurrentShop.Place.Id;
            var shop = S.PlayerShop;
            double Draft() => _multiplierDrafts.TryGetValue(key, out var previous) ? previous : state.MenuMarkup;
            double draft = Draft();
            var label = Line(C, "", 17, Ui.Good);
            Button save = null;
            void Preview()
            {
                label.text = $"菜单售价 {draft:0.0}× · 购买意愿 {shop.MarkupPurchaseIntent(draft) * 100:0}%";
                if (save != null) save.interactable = Math.Abs(draft - state.MenuMarkup) > .001;
            }
            var slider = Ui.Slider(C, .8f, (float)PCShopSell.Core.Shop.ShopSettings.MaxMarkup, (float)draft, value =>
            {
                draft = Math.Round(value * 10) / 10;
                _multiplierDrafts[key] = draft;
                Preview();
            });
            slider.name = "MenuMarkupSlider";
            save = Ui.Size(Ui.Button(C, "确认菜单售价", () =>
            {
                state.MenuMarkup = draft;
                _multiplierDrafts.Remove(key);
                slider.SetValueWithoutNotify((float)draft);
                refreshRecipes();
                Preview();
            }, 17, Ui.ButtonHot, false), -1, 36);
            save.name = "SaveMenuMarkup";
            Line(C,"供应开关即时生效，调整价格后请确认。",14,Ui.Dim);
            return () =>
            {
                draft = Draft();
                slider.SetValueWithoutNotify((float)draft);
                Preview();
            };
        }
        private void BuildSpecialOrders()
        {
            var state=S.SpecialBusiness(S.CurrentShop.Place.Id);
            if(S.RetailKind=="secondhand")
            {
                CounterText(() => $"累计成交 {state.Paid} 单 · 营业收入 ¥{state.Revenue}", 19, Ui.Accent);
                Line(C,"收购与售出记录",18,Ui.Accent);
                foreach (var item in state.Used.Where(i => i.Owned).Reverse())
                {
                    if (item.Sold) BuildUsedSale(C, item);
                    else Line(C, $"{UsedItemTitle(item)} · {(item.Listed ? "待售" : "处理中")} · 收购成本 ¥{item.Cost}", 16);
                }
                if(!state.Used.Any(i=>i.Owned))Line(C,"暂无收购记录。",16,Ui.Dim);
                return;
            }
            var fishOrders = state.Orders.Where(o => o.IsFishOrder).OrderBy(o => o.DueAt).ThenBy(o => o.Id).ToArray();
            Line(C, $"鱼餐预订 {fishOrders.Length} / {GameSession.RestaurantFishOrderLimit}", 19, Ui.Accent);
            if (fishOrders.Length == 0) Line(C, "当前没有鱼餐预订。", 16, Ui.Dim);
            else Line(C, "厨师按交期优先制作备齐的预订。出锅后可交付收款。", 15, Ui.Dim);
            foreach (var order in fishOrders) BuildFishOrder(state, order);

            var tableOrders = state.Orders.Where(o => !o.IsFishOrder && !o.TableCleared).OrderBy(o => o.Table).ToArray();
            Line(C, $"当前桌单 {tableOrders.Length} 单", 19, Ui.Accent);
            foreach (var order in tableOrders)
            {
                string status = order.Stage switch
                {
                    DiningStage.Waiting => "待备料",
                    DiningStage.Cooking => "烹饪中",
                    DiningStage.Ready => "待上菜",
                    DiningStage.Eating => "用餐中",
                    DiningStage.Checkout => "待收桌收款",
                    DiningStage.Completed => "已付款 · 待收桌",
                    DiningStage.Burnt => "已焦糊",
                    _ => "顾客已离开 · 待收桌"
                };
                ItemLabel(C, "recipe:" + order.Recipe, $"{order.Table + 1}号桌 · {order.DishName} · ¥{order.Price} · {status}", 17);
                if (order.Stage == DiningStage.Waiting)
                {
                    var stock = Para(C, "", 15, Ui.Warn);
                    void RefreshIngredients()
                    {
                        string blocker = S.RestaurantIngredientBlocker(S.CurrentShop, order);
                        stock.text = blocker ?? "";
                        stock.gameObject.SetActive(blocker != null);
                    }
                    RefreshIngredients();
                    _counterRefresh.Add(RefreshIngredients);
                }
            }
            if (tableOrders.Length == 0) Line(C, "当前没有桌单。", 16, Ui.Dim);
            Ui.Size(Ui.Button(C,"返回柜台处理桌单",()=>SelectTab(ShopTab.Counter),17),-1,38);
        }

        private void BuildFishOrder(SpecialBusinessState state, DiningOrder order)
        {
            var fish = order.Fish;
            ItemLabel(C, fish?.Id ?? "meal:fish", $"{order.DishName} · ¥{order.Price:N0} · 交付名望 +{order.ReputationReward:0.##}", 18, color: Ui.Accent);
            Line(C, $"交期 {new GameTimestamp(order.DueAt)} · {fish?.Rarity}", 15, Ui.Dim);
            var worker = state.Workers.FirstOrDefault(w => w.Busy && w.OrderId == order.Id);
            bool canWork = !state.Workers.Any(w => w.Busy && w.ActorId == GameSession.RestaurantPlayer);
            if (worker != null)
            {
                string who = worker.ActorId == GameSession.RestaurantPlayer ? "你" : S.FindEmployee(worker.ActorId)?.Person.Name ?? "厨师";
                string progress = worker.OperateUntil < 0 ? "正前往灶台" : "正在" + GameSession.RestaurantJobName(worker.Job);
                Line(C, who + " · " + progress, 16, Ui.Good);
                return;
            }

            var row = Ui.Row(C, 36, 5);
            void Request(RestaurantJob job) { _ctrl.RequestRestaurantWork(order.Id, job); RebuildDock(); }
            var now = S.Clock.Now.TotalMinutes;
            switch (order.Stage)
            {
                case DiningStage.Waiting:
                    var cook = Ui.Size(Ui.Button(row, "备料投锅", () => Request(RestaurantJob.Cook), 16, Ui.ButtonBg), -1, 36, 1);
                    var venue = S.City.HasPlace(GameSession.FishingVenueId) ? S.City.GetPlace(GameSession.FishingVenueId) : null;
                    bool hasFishInBackpack = false;
                    var source = Ui.Size(Ui.Button(row, "去钓鱼", () =>
                    {
                        if (hasFishInBackpack) OpenFridgeWindow();
                        else if (venue != null)
                        {
                            CloseAllWindows();
                            _ctrl.GoToPlace(venue);
                        }
                    }, 16, Ui.ButtonHot), -1, 36, 1);
                    var sourceLabel = source.GetComponentInChildren<Text>();
                    Ui.Size(Ui.Button(row, "拒绝预订", () => { S.DeclineRestaurantFishOrder(order.Id); RebuildDock(); }, 16), -1, 36, 1);
                    var stock = Para(C, "", 15, Ui.Dim);
                    stock.transform.SetSiblingIndex(row.GetSiblingIndex());
                    void RefreshIngredients()
                    {
                        var (missing, missingFish) = S.RestaurantOrderIngredients(S.CurrentShop, order);
                        hasFishInBackpack = missingFish && S.Backpack.Food.IngredientCount(fish.Id) > 0;
                        stock.text = hasFishInBackpack ? missing + $"背包已有{fish.Name}，可以存进门店冰箱。"
                            : missing ?? $"备料齐全 · {fish?.Name} 1 条";
                        stock.color = missing == null ? Ui.Dim : Ui.Warn;
                        cook.interactable = canWork && missing == null;
                        sourceLabel.text = hasFishInBackpack ? "打开冰箱" : "去钓鱼";
                        source.gameObject.SetActive(missingFish && (hasFishInBackpack || venue != null));
                    }
                    RefreshIngredients();
                    _counterRefresh.Add(RefreshIngredients);
                    break;
                case DiningStage.Cooking:
                    Ui.Size(Ui.Button(row, now < order.ReadyAt ? "烹饪中" : "出锅", () => Request(RestaurantJob.Plate),
                        16, Ui.ButtonBg, canWork && now >= order.ReadyAt), -1, 36, 1);
                    break;
                case DiningStage.Ready:
                    Ui.Size(Ui.Button(row, "交付并收款", () => { S.HandInRestaurantFishOrder(order.Id); RebuildDock(); },
                        16, Ui.ButtonHot), -1, 36, 1);
                    break;
                case DiningStage.Burnt:
                    Ui.Size(Ui.Button(row, "焦糊 · 倒掉重做", () => Request(RestaurantJob.Clear), 16, Ui.ButtonBg, canWork), -1, 36, 1);
                    break;
            }
        }
    }
}
