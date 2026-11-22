using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Session;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Shop;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private static readonly string[] UsedDeskTabs = { "待收购", "待鉴定", "待上架" };
        private Window _openingUsedDeskWindow;
        private Button _openingUsedDeskButton, _openingUsedDeskAction;
        private readonly Dictionary<(string Shop, int Item), long> _usedPriceDrafts =
            new Dictionary<(string Shop, int Item), long>();

        private static UsedItem[][] PendingUsedItems(SpecialBusinessState state)
        {
            var pending = state.Used.Where(item => !item.Sold && !item.Listed).ToLookup(item =>
                item.WaitingForPurchase ? 0 : item.Inspected ? 2 : 1);
            return Enumerable.Range(0, UsedDeskTabs.Length).Select(tab => pending[tab].ToArray()).ToArray();
        }

        private static string UsedItemTitle(UsedItem item) => $"{GameSession.UsedItemName(item)} #{item.Id}";

        private static string UsedSellerName(UsedItem item) =>
            GameSession.SellerNames[Math.Max(0, Math.Min(GameSession.SellerNames.Length - 1, item.SellerId))];

        private static string UsedDelegationStatus(bool hasAppraiser, SpecialBusinessState state) =>
            !hasAppraiser ? "未安排收购师" : state.AutoPurchasePaused ? "收购师代管已暂停" : "收购师代管已开启";

        private void BuildUsedCounter(SpecialBusinessState state)
        {
            var pending = PendingUsedItems(state);
            var card = Ui.Panel(C, "UsedCounterOverview", Ui.PanelBg2).rectTransform;
            Ui.VList(card, 5, 8);
            var heading = Ui.Row(card, 30);
            Ui.Size(Line(heading, "旧物收购", 20, Ui.Accent), -1, 30, 1);
            Ui.Size(Ui.Button(heading, "?", OpenUsedHelp, 15), 32, 30).Tip("二手收购说明");
            var totals = Ui.Row(card, 30);
            for (int i = 0; i < UsedDeskTabs.Length; i++)
            {
                var cell = Ui.Size(Ui.Panel(totals, "UsedPendingCount", Ui.PanelBg), -1, 30, 1);
                var label = Ui.Label(cell.transform, $"{UsedDeskTabs[i]} {pending[i].Length}", 16, TextAnchor.MiddleCenter);
                label.rectTransform.Fill();
            }
            var openDesk = Ui.Size(Ui.Button(card, "打开收购鉴定台  ›", OpenUsedDesk, 17, Ui.ButtonHot), -1, 36);
            openDesk.name = "OpenUsedDesk";
            _openingUsedDeskButton = openDesk;
            if (pending[0].Length > 0)
            {
                var dot = Ui.Label(openDesk.transform, "●", 18, TextAnchor.MiddleCenter, Ui.FlatDown);
                dot.name = "UsedSellerWaitingDot";
                dot.rectTransform.Anchor(1, .5f, 1, .5f, -32, -12, 12, -12);
                dot.gameObject.AddComponent<Outline>().effectColor = Ui.PanelBg;
            }
            int repairing = state.Used.Count(i => i.RepairWorkRemaining > 0 && !i.Sold);
            if (repairing > 0 || S.CurrentShop.Layout.Any(p => p.Id == "used_repairbench"))
                Ui.Size(Ui.Button(card, $"修复台 · 处理中 {repairing} 件  ›", OpenUsedRepairDesk, 16), -1, 32);
            if (S.CarriedMarketFindCount > 0)
                Ui.Size(Ui.Button(card, $"淘来旧物入库 · {S.CarriedMarketFindCount} 件",
                    () => OpenMarketFindIntake(), 17, Ui.ButtonHot), -1, 35);
            BuildUsedDelegation(card, state);
            BuildUsedCheckout();
        }

        private void OpenUsedHelp() =>
            OpenWorkspace("help", "二手收购说明", "二手收购说明", 720, 580, w =>
                Para(w.Content, "收购后鉴定，再定价上架。摆放二手修复台并安排修复师，可付耗材尝试修复瑕疵或损坏旧物；每件一次，失败不退耗材。动手能力更影响速度，智力更影响成功率。销售优先推介高价及久置旧物，顾客仍需正常付款。", 18));

        private void BuildUsedDelegation(Transform parent, SpecialBusinessState state)
        {
            var settings = Ui.Panel(parent, "UsedDelegationSettings", Ui.PanelBg).rectTransform;
            Ui.VList(settings, 6, 8);
            var header = Ui.Row(settings, 30, 8);
            Ui.Size(Ui.Label(header, "代管设置", 17, color: Ui.Accent), 76, 30);
            var summary = Ui.Size(Ui.Label(header, "", 15, color: Ui.Dim), 0, 30, 1);
            bool HasAppraiser() => S.ShopStaff(S.CurrentShop, EmployeeRole.Appraiser).Any();
            var toggle = Ui.Size(Ui.Button(header, "", () =>
            {
                if (!HasAppraiser()) return;
                state.AutoPurchasePaused = !state.AutoPurchasePaused;
                RebuildDock();
            }, 15), 94, 30);
            toggle.name = "UsedDelegationToggle";
            var toggleLabel = toggle.GetComponentInChildren<Text>();
            void RefreshSummary()
            {
                bool hasAppraiser = HasAppraiser();
                bool active = hasAppraiser && !state.AutoPurchasePaused;
                summary.text = $"{UsedDelegationStatus(hasAppraiser, state)} · 今日已花 ¥{state.SpentToday:N0}";
                toggle.interactable = hasAppraiser;
                Ui.SetButtonFill(toggle, hasAppraiser && !active ? Ui.ButtonHot : Ui.ButtonBg);
                toggleLabel.color = hasAppraiser ? Ui.TextColor : Ui.Dim;
                toggleLabel.text = active ? "暂停代管" : "开启代管";
            }
            RefreshSummary();
            _counterRefresh.Add(RefreshSummary);

            var amounts = Ui.Row(settings, 60, 12);
            void Amount(string label, string key, long value, long step, long max, Action<long> commit)
            {
                var column = Ui.Size(Ui.Rect(amounts, key), 0, 60, 1);
                Ui.VList(column, 2, 0);
                Ui.Size(Ui.Label(column, label + "（¥）", 16), -1, 24);
                UsedAmountEditor(column, null, value, step, 0, max, commit, key + "Input");
            }
            Amount("每日收购预算", "UsedDailyBudget", state.DailyBudget, 100, 100000, value => state.DailyBudget = value);
            Amount("单件收购上限", "UsedMaxOffer", state.MaxOffer, 10, 10000, value => state.MaxOffer = value);

            var risk = Ui.Row(settings, 34);
            Ui.Size(Ui.Label(risk, "出价倾向", 16), 88, 34).Tip("普通旧物按估值预留毛利：谨慎约41%、均衡35%、积极29%，并参考卖家要价议价。估值可能出错，不保证实际利润。文物沿用独立报价规则；均受预算、单件上限和现金限制。");
            double current = state.Risk < .4 ? .2 : state.Risk > .6 ? .8 : .5;
            foreach (var (label, level) in new[] { ("谨慎", .2), ("均衡", .5), ("积极", .8) })
                Ui.Size(Ui.Button(risk, label, () =>
                {
                    state.Risk = level;
                    RebuildDock();
                }, 16, Ui.SelectedButton(current == level)), 0, 34, 1).name = "UsedRisk_" + label;
        }

        public void OpenUsedRepairDesk()
        {
            if (!S.InPlayerShop || S.RetailKind != "secondhand") return;
            var shop = S.CurrentShop;
            var state = S.SpecialBusiness(shop.Place.Id);
            OpenWindow("二手修复台", 940, 560, w =>
            {
                var items = state.Used
                    .Where(item => item.Owned && !item.Sold && (item.RepairAttempted
                        || (!item.Listed && GameSession.UsedCanRepair(item))))
                    .OrderBy(item => item.RepairWorkRemaining > 0 ? 0 : item.RepairAttempted ? 2 : 1)
                    .ThenBy(item => item.Id)
                    .ToArray();
                if (items.Length == 0)
                {
                    Line(w.Content, "暂无可修复的旧物。鉴定出有瑕疵或损坏的旧物后，可在这里安排修复。", 17, Ui.Dim);
                    return;
                }

                foreach (var item in items)
                {
                    var row = Ui.Row(w.Content, 64, 10);
                    ItemPortrait(row, GameSession.UsedItemPortrait(item), 46);
                    var info = Ui.Size(Ui.Rect(row, "UsedRepairItem_" + item.Id), 0, 64, 1);
                    Ui.VList(info, 2, 3);
                    Line(info, UsedItemTitle(item) + " · " + GameSession.UsedConditionName(item), 18);
                    if (item.RepairAttempted)
                    {
                        string status = S.UsedRepairStatus(shop, item);
                        if (item.RepairWorkRemaining <= 0) status += item.Listed ? " · 已上架" : " · 待上架";
                        Line(info, status + $" · 已付耗材 ¥{item.RepairCost:N0}", 16, Ui.Dim);
                        continue;
                    }

                    string blocker = S.UsedRepairBlocker(shop, item);
                    Line(info, blocker ?? "待修复", 16, blocker == null ? Ui.Dim : Ui.Warn);
                    Ui.Size(UsedRepairButton(row, item, blocker, () =>
                    {
                        RebuildDock();
                        w.Rebuild();
                    }), 214, 38);
                }
            });
        }

        private Button UsedRepairButton(Transform parent, UsedItem item, string blocker, Action onStarted)
        {
            var button = Ui.Button(parent, $"安排修复 · 耗材 ¥{GameSession.UsedRepairMaterialCost(item):N0}",
                () =>
                {
                    if (S.StartUsedRepair(item.Id)) onStarted();
                }, 16, Ui.ButtonHot, blocker == null);
            return button.Tip(blocker ?? "每件仅可尝试一次；失败保留原状态，不退耗材。修复师到岗后工作。");
        }

        public void OpenUsedDesk()
        {
            if (!S.InPlayerShop || S.RetailKind != "secondhand") return;
            var state = S.SpecialBusiness(S.CurrentShop.Place.Id);
            var lessonItem = S.OpeningUsedDeskLessonItem();
            int tab = lessonItem == null ? Math.Max(0, Array.FindIndex(PendingUsedItems(state), items => items.Length > 0))
                : !lessonItem.Owned ? 0 : !lessonItem.Inspected ? 1 : 2;
            int selectedId = lessonItem?.Id ?? 0;
            OpenWindow("收购鉴定台", 900, 550, w =>
            {
                if (lessonItem != null)
                {
                    _openingUsedDeskWindow = w;
                    _openingUsedDeskAction = null;
                }
                var pending = PendingUsedItems(state);
                void Select(int nextTab, int itemId = 0)
                {
                    if (nextTab != tab) w.ResetScrollOnRebuild = true;
                    tab = nextTab;
                    selectedId = itemId;
                    w.Rebuild();
                }

                if (S.CarriedMarketFindCount > 0)
                    Ui.Size(Ui.Button(w.Content, $"淘来旧物入库 · {S.CarriedMarketFindCount} 件",
                        () => OpenMarketFindIntake(() => Select(2)), 18, Ui.ButtonHot), -1, 42);

                var tabs = Ui.Row(w.Content, 40);
                for (int i = 0; i < UsedDeskTabs.Length; i++)
                {
                    int index = i;
                    Ui.Size(Ui.Button(tabs, $"{UsedDeskTabs[i]}  {pending[i].Length}", () => Select(index), 17,
                        Ui.SelectedButton(i == tab)), -1, 40, 1);
                }

                var items = pending[tab];
                var selected = items.FirstOrDefault(item => item.Id == selectedId) ?? items.FirstOrDefault();
                if (selected == null)
                {
                    string empty = tab == 0 ? "当前没有等待报价的卖家。" : tab == 1 ? "当前没有待鉴定的旧物。" : "当前没有待上架的旧物。";
                    Line(w.Content, empty, 17, Ui.Dim);
                    return;
                }

                selectedId = selected.Id;
                bool guided = lessonItem == selected && S.OpeningUsedDeskLessonItem() == selected;
                var body = Ui.Row(w.Content, 350, 10);
                var list = Ui.Scroll(body);
                Ui.Size((RectTransform)list.parent.parent, 258, 350);
                for (int i = 0; i < items.Length; i++)
                {
                    var item = items[i];
                    string subtitle = tab == 0 ? $"第 {i + 1} 位 · {UsedSellerName(item)} · 要价 ¥{item.Asking:N0}" :
                        tab == 1 ? $"收购成本 ¥{item.Cost:N0}" : GameSession.UsedConditionName(item);
                    var entry = Ui.Size(Ui.Button(list, $"{UsedItemTitle(item)}\n{subtitle}",
                        () => Select(tab, item.Id), 16, Ui.SelectedButton(item.Id == selectedId)), -1, 60);
                    entry.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
                    ItemButtonPortrait(entry, GameSession.UsedItemPortrait(item), 42);
                }

                var detail = Ui.Size(Ui.Panel(body, "UsedItemDetail", Ui.PanelBg2), -1, 350, 1).rectTransform;
                BuildUsedItemAction(detail, selected, guided, nextTab =>
                {
                    RebuildDock();
                    if (guided && selected.Listed)
                    {
                        S.CompleteOpeningUsedDeskLesson();
                        w.Close();
                        return;
                    }
                    Select(nextTab, selected.Id);
                });
            }, scrollContent: false);
        }

        private void BuildUsedItemAction(RectTransform detail, UsedItem item, bool guided, Action<int> refresh)
        {
            var parent = Ui.Scroll(detail);
            ((RectTransform)parent.parent.parent).Anchor(0, 0, 1, 1, 14, 64, 14, 14);
            var actions = Ui.Rect(detail, "UsedItemActions").Anchor(0, 0, 1, 0, 14, 14, 14, -56);
            int id = item.Id;
            void Primary(string name, string caption, Action action, bool enabled = true)
            {
                var button = Ui.Button(actions, caption, action, 17, Ui.ButtonHot);
                button.name = name + id;
                button.interactable = enabled;
                button.image.rectTransform.Anchor(0, 0, 1, 1, 0, 0, 96);
                if (guided) _openingUsedDeskAction = button;
            }
            Button Secondary(string caption, Action action)
            {
                var button = Ui.Button(actions, caption, action, 16, Ui.Warn);
                button.image.rectTransform.Anchor(1, 0, 1, 1, -90);
                return button;
            }
            var key = (S.CurrentShop.Place.Id, id);
            void Complete(bool succeeded, int nextTab)
            {
                if (!succeeded) return;
                _usedPriceDrafts.Remove(key);
                refresh(nextTab);
            }
            long Draft(long initial) => _usedPriceDrafts.TryGetValue(key, out long draft) ? draft : initial;
            void SaveDraft(long amount) => _usedPriceDrafts[key] = amount;

            ItemLabel(parent, GameSession.UsedItemPortrait(item), UsedItemTitle(item), 23, 48, Ui.Accent);
            if (!item.Owned)
            {
                Line(parent, $"{UsedSellerName(item)} · {GameSession.SellerPersonalities[item.Personality]}", 17);
                var quote = Ui.Panel(parent, "SellerQuote", Ui.PanelBg).rectTransform;
                Ui.VList(quote, 0, 8);
                Para(quote, GameSession.SellerSpeech(item), 17);
                Line(parent, $"卖家要价 ¥{item.Asking:N0} · 状态未鉴定", 17, Ui.Warn);
                if (item.ArtifactAssessmentMade)
                    Line(parent, item.ArtifactIdentified ? "收购师判断 · " + GameSession.UsedConditionName(item)
                        : "收购师暂时看不准真伪。", 16, Ui.Dim);
                var bid = UsedAmountEditor(parent, "我的报价", guided ? item.Asking : Draft(item.Asking), 1, 1, 1000000, SaveDraft);
                Primary("BuyUsed_", "出价收购", () =>
                {
                    if (!S.OfferUsedPrice(id, bid())) return;
                    if (guided) S.RecordOpeningUsedDeskPurchase(id);
                    Complete(true, 1);
                });
                Secondary("拒收", () => Complete(S.DeclineUsedSeller(id), 0))
                    .name = "DeclineUsed_" + id;
                return;
            }
            if (!item.Inspected)
            {
                Line(parent, $"收购成本 ¥{item.Cost:N0} · 尚未鉴定", 17, Ui.Dim);
                Primary("InspectUsed_", "检查状态", () => Complete(S.InspectUsed(id), 2));
                return;
            }

            Line(parent, GameSession.UsedConditionName(item), 19, Ui.Good);
            Line(parent, $"总成本 ¥{item.Cost:N0} · 参考转售价 ¥{GameSession.UsedValue(item):N0}", 17);
            if (item.RepairWorkRemaining > 0)
            {
                Line(parent, S.UsedRepairStatus(S.CurrentShop, item), 17, Ui.Accent);
                Para(parent, "关闭窗口后随世界时间继续。休息或修复台收起时暂停，重新到岗后接着做。", 16, Ui.Dim);
                Primary("RepairingUsed_", "修复中 · 完成后可出售", () => { }, false);
                return;
            }
            if (item.RepairAttempted)
                Line(parent, S.UsedRepairStatus(S.CurrentShop, item) + $" · 已付耗材 ¥{item.RepairCost:N0}", 16, Ui.Dim);
            else if (GameSession.UsedCanRepair(item))
            {
                string blocker = S.UsedRepairBlocker(S.CurrentShop, item);
                Ui.Size(UsedRepairButton(parent, item, blocker, () => Complete(true, 2)), -1, 36);
            }
            if (GameSession.UsedSaleReputation(item) > 0)
                Line(parent, $"顾客付款后 · 名望 +{GameSession.UsedSaleReputation(item):0.#}", 16, Ui.Good);
            var intent = Line(parent, "", 16, Ui.Good);
            var price = UsedAmountEditor(parent, "出售标价", Draft(Math.Max(1, item.Price)), 1, 1,
                GameSession.UsedListingPriceLimit(item), SaveDraft,
                preview: amount => intent.text = $"购买意愿 {PurchaseEstimateText(UsedPurchaseIntent(item, amount))}");
            int NextQueuedTab()
            {
                var pending = PendingUsedItems(S.SpecialBusiness(S.CurrentShop.Place.Id));
                if (pending[0].Length > 0) return 0;
                return pending[1].Length > 0 ? 1 : 2;
            }
            Primary("ListUsed_", "确认售价并上架", () =>
            {
                if (TryListUsedItem(id, price())) Complete(true, NextQueuedTab());
            });
            Secondary("丢弃", () => ConfirmBusiness("丢弃旧物", "将损失这件旧物及收购成本。",
                () => Complete(S.DiscardUsed(id), 2)));
        }

        private bool TryListUsedItem(int id, long price)
        {
            if (S.ListUsedItem(id, price, out var blocker)) return true;
            Popup("暂时无法上架", blocker);
            return false;
        }

        private static Func<long> UsedAmountEditor(Transform parent, string label, long amount, long step,
            long min, long max, Action<long> commit, string inputName = null, Action<long> preview = null)
        {
            var row = Ui.Row(parent, 34, 4);
            if (label != null) Ui.Size(Ui.Label(row, label + " ¥", 16), -1, 34, 1);
            var input = Ui.TextInput(row, amount.ToString(), "金额", null);
            input.contentType = InputField.ContentType.IntegerNumber;
            input.characterLimit = 10;
            Ui.Size(input, label == null ? 0 : 105, 34, label == null ? 1 : -1);
            if (inputName != null) input.name = inputName;

            long Save(long number)
            {
                amount = Math.Max(min, Math.Min(max, number));
                input.SetTextWithoutNotify(amount.ToString());
                commit?.Invoke(amount);
                preview?.Invoke(amount);
                return amount;
            }
            long Read() => long.TryParse(input.text, out long number) ? Math.Max(min, Math.Min(max, number)) : amount;
            input.onEndEdit.AddListener(_ => Save(Read()));
            if (preview != null)
            {
                input.onValueChanged.AddListener(_ => preview(Read()));
                preview(Read());
            }
            Ui.Size(Ui.Button(row, "−", () => Save(Read() - step), 18), 40, 34);
            Ui.Size(Ui.Button(row, "+", () => Save(Read() + step), 18), 40, 34);
            input.transform.SetSiblingIndex(label == null ? 1 : 2);
            return Read;
        }

        private void BuildUsedCheckout()
        {
            if (S.PlayerShop.Queue.Count == 0)
            {
                Line(C, "暂无顾客排队", 16, Ui.Dim);
                return;
            }
            foreach(var customer in S.PlayerShop.Queue)
            {
                int id=customer.Id;var row=Ui.Row(C,58,5);
                Ui.Size(Ui.Label(row,customer.ItemName+"\n应收 "+customer.Spend,16),-1,58,1);
                var checkout=Ui.Button(row,"结账",()=>_ctrl.Checkout(id),17,Ui.ButtonHot,_ctrl.CanRequestCheckout);
                Ui.Size(checkout,90,40).name="UsedCheckout_"+id;
                _openingCounterButtons[id] = checkout;
                _counterRefresh.Add(()=>checkout.interactable=_ctrl.CanRequestCheckout);
            }
            CounterText(CounterCheckoutHint, 14, Ui.Dim);
        }
        private void BuildUsedSalesHistory(Transform parent,SpecialBusinessState state)
        {
            foreach (var sold in state.Used.Where(i => i.Sold).Reverse().Take(5))
                BuildUsedSale(parent, sold);
        }
        private static void BuildUsedSale(Transform parent, UsedItem sold)
        {
            long price = sold.SalePrice > 0 ? sold.SalePrice : sold.Price;
            double reputation = GameSession.UsedSaleReputation(sold);
            string reward = reputation > 0 ? $" · 名望 +{reputation:0.#}" : "";
            Line(parent, $"已售出 · {UsedItemTitle(sold)} · 收入 ¥{price:N0} · 成本 ¥{sold.Cost:N0} · 毛利 ¥{price - sold.Cost:N0}{reward}",
                15, price >= sold.Cost ? Ui.Good : Ui.Warn).name = "UsedSold_" + sold.Id;
        }
        private void BuildUsedStock(Transform parent, Window owner = null, bool shelfOnly = false, bool pricingOnly = false,
            int[] displayed = null, int shelfPage = 0, Action<int> changeShelfPage = null)
        {
            var state=S.SpecialBusiness(S.CurrentShop.Place.Id);
            var onShelf=S.PlayerShop.ShelfFillOf(ShelfKinds.Get("secondhand")).Resale.Select(i=>i.Id).ToArray();
            Line(parent,pricingOnly?"二手商品定价":"二手转售库存",19,Ui.Accent);
            var items=state.Used.Where(i=>i.Owned&&!i.Sold&&(!shelfOnly||(displayed??onShelf).Contains(i.Id))&&(!pricingOnly||i.Inspected)).ToArray();
            if (pricingOnly)
            {
                var targets = S.UsedUniformPriceTargets();
                var summary = Ui.Row(parent, 38, 8);
                string estimate = targets.Length > 0
                    ? $"陈列商品平均购买意愿 {PurchaseEstimateText(targets.Average(item => UsedPurchaseIntent(item, item.Price)))}"
                    : "暂无可统一定价的陈列商品";
                Ui.Size(Ui.Label(summary, estimate, 16, color: Ui.Good), 0, 38, 1)
                    .Tip("按可购买的陈列商品平均。购买意愿为顾客看到这件商品后的估算，结账还受付款方式影响。");
                Ui.Size(Ui.Button(summary, "统一定价", OpenUsedUniformPricing, 17, Ui.ButtonHot, targets.Length > 0), 120, 36)
                    .name = "OpenUsedUniformPricing";
            }
            const int pageSize = 8;
            int pages = Math.Max(1, (items.Length + pageSize - 1) / pageSize);
            shelfPage = Mathf.Clamp(shelfPage, 0, pages - 1);
            Transform container=parent;
            if (shelfOnly && items.Length > 0)
            {
                var frame = Ui.FlatPanel(parent, "UsedShelf", Ui.FlatBand).rectTransform;
                Ui.VList(frame, 8, 10);
                container = frame;
                items = items.Skip(shelfPage * pageSize).Take(pageSize).ToArray();
            }
            RectTransform shelfRow = null;
            int shelfIndex = 0;
            foreach(var item in items)
            {
                int id=item.Id;bool reserved=S.UsedReserved(S.CurrentShop,id);
                string status=reserved?"顾客已拿取 · 等待收银":onShelf.Contains(id)?"已上架 · 等待挑选":item.Listed?"等待货架空位":item.Inspected?"已鉴定 · 待定价上架":"待鉴定";
                if (shelfOnly && shelfIndex++ % 2 == 0) shelfRow = Ui.Row(container, 148, 10);
                var card = (shelfOnly ? Ui.Size(Ui.FlatPanel(shelfRow, "UsedStock_" + id, Ui.PanelBg), 0, 148, 1)
                    : Ui.Panel(container, "UsedStock_" + id, Ui.PanelBg2)).rectTransform;
                Ui.VList(card, 5, 8);
                var title = Ui.Row(card, 46, 8);
                ItemPortrait(title, GameSession.UsedItemPortrait(item), 42).name = "UsedIllustration_" + id;
                Ui.Size(Ui.Label(title, GameSession.UsedItemName(item) + " #" + id + " · " + status, 17, color: Ui.Accent), 0, -1, 1);
                Line(card,(item.Inspected?GameSession.UsedConditionName(item):"状态待鉴定")+" · "+GameSession.UsedWeight(item).ToString("0.##")+" 千克 · 成本 ¥"+item.Cost+" · 标价 ¥"+item.Price,15);
                var row=Ui.Row(card,34,5);
                if (pricingOnly)
                {
                    string intent = reserved ? "已锁定成交价" : $"购买意愿 {PurchaseEstimateText(UsedPurchaseIntent(item, item.Price))}";
                    Ui.Size(Ui.Label(row, intent, 16, color: reserved ? Ui.Dim : Ui.Good), 0, 34, 1).name = "UsedPurchaseIntent_" + id;
                }
                Ui.Size(Ui.Button(row,item.Listed?"调整售价":"定价并上架",()=>OpenUsedPrice(id,owner),16,Ui.ButtonBg,
                    item.Inspected&&!reserved && item.RepairWorkRemaining <= 0), pricingOnly ? 110 : -1, 34, pricingOnly ? -1 : 1);
                if(item.Listed)Ui.Size(Ui.Button(row,"下架",()=>{S.UnlistUsedItem(id);owner?.Rebuild();RebuildDock();},16,Ui.ButtonBg,!reserved),85,34);
            }
            if (shelfOnly && shelfIndex % 2 != 0) Ui.Size(Ui.Rect(shelfRow, "EmptyUsedShelfSlot"), 0, 148, 1);
            if(items.Length==0)Line(parent,"暂无二手转售库存。",16,Ui.Dim);
            if (shelfOnly)
            {
                BuildCompactPageControls(parent, shelfPage, pages, changeShelfPage);
                var sales = state.Used.Where(i => i.Sold).Reverse().Take(5).ToArray();
                if (sales.Length > 0) BuildUsedSale(parent, sales[0]);
                if (sales.Length > 1)
                    Ui.Size(Ui.Button(parent, "查看最近成交", () => OpenCompactWindow("最近成交", 780,
                        w => BuildUsedSalesHistory(w.Content, state)), 16), -1, 34);
            }
            else if (!pricingOnly) BuildUsedSalesHistory(parent, state);
        }
        private void OpenMarketFindIntake(Action refresh = null)
        {
            if (!S.InPlayerShop || S.RetailKind != "secondhand") return;
            OpenWindow("淘来旧物入库", 840, 520, w =>
            {
                var items = S.CarriedMarketFinds;
                if (items.Length == 0)
                {
                    Line(w.Content, "背包里已没有待入库的旧物。到收购鉴定台的「待上架」定价。", 18);
                    Ui.Size(Ui.Button(w.Content, "完成", w.Close, 18, Ui.ButtonHot), -1, 44);
                    return;
                }
                foreach (var item in items)
                {
                    var row = Ui.Row(w.Content, 64, 10);
                    ItemPortrait(row, GameSession.UsedItemPortrait(item), 54);
                    string details = $"{GameSession.UsedConditionName(item)} · 参考价 ¥{GameSession.UsedValue(item):N0}";
                    Ui.Size(ProductText(row, $"<b>{GameSession.UsedItemName(item)}</b>\n<size=15>{details}</size>", 18),
                        0, 60, 1);
                    Ui.Size(Ui.Button(row, "入库", () =>
                    {
                        if (!S.StoreMarketFindAtUsedShop(item.Id)) return;
                        RebuildDock();
                        w.Rebuild();
                    }, 18, Ui.ButtonHot), 100, 42);
                }
            }, onClosed: refresh);
        }

        private double UsedPurchaseIntent(UsedItem item, long price) =>
            S.PlayerShop.ResalePurchaseIntentAtPrice(GameSession.UsedValue(item), price);

        private void OpenUsedUniformPricing()
        {
            var items = S.UsedUniformPriceTargets();
            if (items.Length == 0) return;
            double multiplier = 1;
            string before = PurchaseEstimateText(items.Average(item => UsedPurchaseIntent(item, item.Price)));
            OpenWindow("二手商品统一定价", 840, Math.Min(780, 260 + items.Length * 46), w =>
            {
                Line(w.Content, "按每件旧物的参考价乘以同一倍率，更新本次陈列商品的标价。", 17);
                var summary = Line(w.Content, "", 18, Ui.Good);
                var labels = new Text[items.Length];
                var slider = Ui.Size(Ui.Slider(w.Content, (float)ShopSettings.MinMarkup, (float)ShopSettings.MaxMarkup,
                    (float)multiplier, value =>
                    {
                        multiplier = Math.Clamp(Math.Round(value * 20) / 20, ShopSettings.MinMarkup, ShopSettings.MaxMarkup);
                        Preview();
                    }), -1, 32);
                slider.name = "UsedUniformPriceSlider";
                var list = Ui.Scroll(w.Content);
                var listSize = Ui.Size((RectTransform)list.parent.parent, -1, 54).GetComponent<LayoutElement>();
                listSize.flexibleHeight = 1;
                for (int i = 0; i < items.Length; i++)
                {
                    var item = items[i];
                    var row = Ui.Row(list, 42, 12);
                    ItemPortrait(row, GameSession.UsedItemPortrait(item), 36);
                    Ui.Size(Ui.Label(row, $"{UsedItemTitle(item)} · {GameSession.UsedConditionName(item)}", 16), 0, 42, 1);
                    labels[i] = Ui.Size(Ui.Label(row, "", 16, TextAnchor.MiddleRight, Ui.Good), 280, 42);
                }
                Ui.Size(Ui.Button(w.Content, $"应用到 {items.Length} 件商品", () =>
                {
                    int applied = S.ApplyUsedUniformPricing(multiplier);
                    if (applied == 0) { Toast("暂无可统一定价的陈列商品。"); return; }
                    w.Close();
                    RebuildDock();
                    Toast($"已统一调整 {applied} 件二手商品的售价。");
                }, 18, Ui.ButtonHot), -1, 42).name = "ConfirmUsedUniformPrice";
                Preview();

                void Preview()
                {
                    double total = 0;
                    for (int i = 0; i < items.Length; i++)
                    {
                        var item = items[i];
                        long price = GameSession.UsedPriceAtMultiplier(item, multiplier);
                        double intent = UsedPurchaseIntent(item, price);
                        total += intent;
                        labels[i].text = $"¥{item.Price:N0} → ¥{price:N0} · 意愿 {PurchaseEstimateText(intent)}";
                    }
                    summary.text = $"参考价 × {multiplier:0.00} · 平均购买意愿 {before} → {PurchaseEstimateText(total / items.Length)}";
                }
            }, scrollContent: false);
        }

        private void OpenUsedPrice(int id,Window owner)
        {
            var item=S.SpecialBusiness(S.CurrentShop.Place.Id).Used.FirstOrDefault(i=>i.Id==id&&!i.Sold);if(item==null)return;
            long price=Math.Max(1,item.Price);
            long priceLimit = GameSession.UsedListingPriceLimit(item);
            OpenWindow("二手商品售价",600,330,w=>
            {
                ItemLabel(w.Content,GameSession.UsedItemPortrait(item),GameSession.UsedItemName(item)+" · "+GameSession.UsedConditionName(item),20,color:Ui.Accent);
                Line(w.Content,"收购成本 ¥"+item.Cost+" · 建议售价 ¥"+GameSession.UsedValue(item),17);
                var estimate = Line(w.Content, "", 17, Ui.Good);
                var readPrice = UsedAmountEditor(w.Content, "售价", price, 1, 1, priceLimit, value => price = value,
                    preview: value => estimate.text = $"预览购买意愿 {PurchaseEstimateText(UsedPurchaseIntent(item, value))}");
                Ui.Size(Ui.Button(w.Content, item.Listed ? "确认售价" : "确认售价并上架", () =>
                {
                    if (!TryListUsedItem(id, readPrice())) return;
                    w.Close();
                    owner?.Rebuild();
                    RebuildDock();
                }, 18, Ui.ButtonHot), -1, 42);
            });
        }
    }
}
