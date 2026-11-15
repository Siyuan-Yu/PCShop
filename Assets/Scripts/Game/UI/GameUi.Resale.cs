using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Session;
using PCShopSell.Core.Inventory;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private static readonly string[] UsedDeskTabs = { "待收购", "待鉴定", "待上架" };
        private readonly Dictionary<(string Shop, int Item), long> _usedPriceDrafts =
            new Dictionary<(string Shop, int Item), long>();

        private static UsedItem[][] PendingUsedItems(SpecialBusinessState state)
        {
            var pending = state.Used.Where(item => !item.Sold && !item.Listed).ToLookup(item =>
                !item.Owned ? 0 : item.Inspected ? 2 : 1);
            return Enumerable.Range(0, UsedDeskTabs.Length).Select(tab => pending[tab].ToArray()).ToArray();
        }

        private static string UsedItemTitle(UsedItem item) => $"{GameSession.UsedNames[item.Product]} #{item.Id}";

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
            Ui.Size(Ui.Button(card, "打开收购鉴定台  ›", OpenUsedDesk, 17, Ui.ButtonHot), -1, 36);
            BuildUsedDelegation(card, state);
            BuildUsedCheckout();
        }

        private void OpenUsedHelp() =>
            OpenWorkspace("help", "二手收购说明", "二手收购说明", 720, 580, w =>
                Para(w.Content, "先听卖家的说法和报价，收购后才能鉴定。鉴定后可自行定价，在二手转售品货架出售。收购师在岗时可按代管设置收购，智力越高越容易判断准确。", 18));

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
                toggle.image.color = hasAppraiser && !active ? Ui.ButtonHot : Ui.ButtonBg;
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
            Ui.Size(Ui.Label(risk, "出价倾向", 16), 88, 34).Tip("越积极，收购师按估价给出的报价越高。报价仍受每日预算和单件上限约束。");
            double current = state.Risk < .4 ? .2 : state.Risk > .6 ? .8 : .5;
            foreach (var (label, level) in new[] { ("谨慎", .2), ("均衡", .5), ("积极", .8) })
                Ui.Size(Ui.Button(risk, label, () =>
                {
                    state.Risk = level;
                    RebuildDock();
                }, 16, current == level ? Ui.ButtonHot : Ui.ButtonBg), 0, 34, 1).name = "UsedRisk_" + label;
        }

        public void OpenUsedDesk()
        {
            if (!S.InPlayerShop || S.RetailKind != "secondhand") return;
            var state = S.SpecialBusiness(S.CurrentShop.Place.Id);
            int tab = Array.FindIndex(PendingUsedItems(state), items => items.Length > 0);
            tab = Math.Max(0, tab);
            int selectedId = 0;
            OpenWindow("收购鉴定台", 900, 550, w =>
            {
                var pending = PendingUsedItems(state);
                void Select(int nextTab, int itemId = 0)
                {
                    if (nextTab != tab) w.ResetScrollOnRebuild = true;
                    tab = nextTab;
                    selectedId = itemId;
                    w.Rebuild();
                }

                var tabs = Ui.Row(w.Content, 40);
                for (int i = 0; i < UsedDeskTabs.Length; i++)
                {
                    int index = i;
                    Ui.Size(Ui.Button(tabs, $"{UsedDeskTabs[i]}  {pending[i].Length}", () => Select(index), 17,
                        i == tab ? Ui.ButtonHot : Ui.ButtonBg), -1, 40, 1);
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
                var body = Ui.Row(w.Content, 350, 10);
                var list = Ui.Scroll(body);
                Ui.Size((RectTransform)list.parent.parent, 258, 350);
                foreach (var item in items)
                {
                    string subtitle = tab == 0 ? $"{UsedSellerName(item)} · 要价 ¥{item.Asking:N0}" :
                        tab == 1 ? $"收购成本 ¥{item.Cost:N0}" : GameSession.ConditionName(item.Condition);
                    var entry = Ui.Size(Ui.Button(list, $"{UsedItemTitle(item)}\n{subtitle}",
                        () => Select(tab, item.Id), 16, item.Id == selectedId ? Ui.ButtonHot : Ui.ButtonBg), -1, 60);
                    entry.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
                }

                var detail = Ui.Size(Ui.Panel(body, "UsedItemDetail", Ui.PanelBg2), -1, 350, 1).rectTransform;
                Ui.VList(detail, 8, 14);
                BuildUsedItemAction(detail, selected, nextTab =>
                {
                    RebuildDock();
                    Select(nextTab, selected.Id);
                });
            }, scrollContent: false);
        }

        private void BuildUsedItemAction(Transform parent, UsedItem item, Action<int> refresh)
        {
            int id = item.Id;
            var key = (S.CurrentShop.Place.Id, id);
            void Complete(bool succeeded, int nextTab)
            {
                if (!succeeded) return;
                _usedPriceDrafts.Remove(key);
                refresh(nextTab);
            }
            long Draft(long initial) => _usedPriceDrafts.TryGetValue(key, out long draft) ? draft : initial;
            void SaveDraft(long amount) => _usedPriceDrafts[key] = amount;

            Line(parent, UsedItemTitle(item), 23, Ui.Accent);
            if (!item.Owned)
            {
                Line(parent, $"{UsedSellerName(item)} · {GameSession.SellerPersonalities[item.Personality]}", 17);
                var quote = Ui.Panel(parent, "SellerQuote", Ui.PanelBg).rectTransform;
                Ui.VList(quote, 0, 8);
                Para(quote, GameSession.SellerSpeech(item), 17);
                Line(parent, $"卖家要价 ¥{item.Asking:N0} · 状态未鉴定", 17, Ui.Warn);
                var bid = UsedAmountEditor(parent, "我的报价", Draft(item.Asking), 1, 1, 1000000, SaveDraft);
                var actions = Ui.Row(parent, 42);
                Ui.Size(Ui.Button(actions, "出价收购", () => Complete(S.OfferUsedPrice(id, bid()), 1), 17, Ui.ButtonHot), -1, 42, 1);
                Ui.Size(Ui.Button(actions, "拒收", () => Complete(S.DeclineUsedSeller(id), 0), 16, Ui.Warn), 90, 42)
                    .name = "DeclineUsed_" + id;
                return;
            }
            if (!item.Inspected)
            {
                Line(parent, $"收购成本 ¥{item.Cost:N0} · 尚未鉴定", 17, Ui.Dim);
                Ui.Size(Ui.Button(parent, "检查状态", () => Complete(S.InspectUsed(id), 2), 17, Ui.ButtonHot), -1, 42);
                return;
            }

            Line(parent, GameSession.ConditionName(item.Condition), 19, Ui.Good);
            Line(parent, $"收购成本 ¥{item.Cost:N0} · 参考转售价 ¥{GameSession.UsedValue(item):N0}", 17);
            var price = UsedAmountEditor(parent, "出售标价", Draft(Math.Max(1, item.Price)), 1, 1, 1000000, SaveDraft);
            var listing = Ui.Row(parent, 42);
            Ui.Size(Ui.Button(listing, "确认售价并上架", () => Complete(TryListUsedItem(id, price()), 2), 17, Ui.ButtonHot), -1, 42, 1)
                .name = "ListUsed_" + id;
            Ui.Size(Ui.Button(listing, "丢弃", () => ConfirmBusiness("丢弃旧物", "将损失这件旧物及收购成本。",
                () => Complete(S.DiscardUsed(id), 2)), 16, Ui.Warn), 90, 42);
        }

        private bool TryListUsedItem(int id, long price)
        {
            if (S.ListUsedItem(id, price, out var blocker)) return true;
            Popup("暂时无法上架", blocker);
            return false;
        }

        private static Func<long> UsedAmountEditor(Transform parent, string label, long amount, long step,
            long min, long max, Action<long> commit, string inputName = null)
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
                commit(amount);
                return amount;
            }
            long Read() => long.TryParse(input.text, out long number) ? Math.Max(min, Math.Min(max, number)) : amount;
            input.onEndEdit.AddListener(_ => Save(Read()));
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
                var checkout=Ui.Button(row,"结账",()=>_ctrl.Checkout(id),17,Ui.ButtonHot,_ctrl.Interior!=null&&_ctrl.Interior.NearCounter);
                Ui.Size(checkout,90,40).name="UsedCheckout_"+id;
                _counterRefresh.Add(()=>checkout.interactable=_ctrl.Interior!=null&&_ctrl.Interior.NearCounter);
            }
            Para(C, "走到收银台旁按 F 或点击结账；也可安排收银员在岗收款。", 14, Ui.Dim);
        }
        private void BuildUsedSalesHistory(Transform parent,SpecialBusinessState state)
        {
            foreach (var sold in state.Used.Where(i => i.Sold).Reverse().Take(5))
                BuildUsedSale(parent, sold);
        }
        private static void BuildUsedSale(Transform parent, UsedItem sold)
        {
            long price = sold.SalePrice > 0 ? sold.SalePrice : sold.Price;
            Line(parent, $"已售出 · {UsedItemTitle(sold)} · 收入 ¥{price:N0} · 成本 ¥{sold.Cost:N0} · 毛利 ¥{price - sold.Cost:N0}",
                15, price >= sold.Cost ? Ui.Good : Ui.Warn).name = "UsedSold_" + sold.Id;
        }
        private void BuildUsedStock(Transform parent,Window owner=null,bool shelfOnly=false,bool pricingOnly=false,int[] displayed=null)
        {
            var state=S.SpecialBusiness(S.CurrentShop.Place.Id);
            var onShelf=S.PlayerShop.ShelfFillOf(ShelfKinds.Get("secondhand")).Resale.Select(i=>i.Id).ToArray();
            Line(parent,pricingOnly?"二手商品定价":"二手转售库存",19,Ui.Accent);
            var items=state.Used.Where(i=>i.Owned&&!i.Sold&&(!shelfOnly||(displayed??onShelf).Contains(i.Id))&&(!pricingOnly||i.Inspected)).ToArray();
            Transform container=parent;
            if(shelfOnly)
            {
                var frame=Ui.Panel(parent,"UsedShelf",WoodDark).rectTransform;Ui.VList(frame,8,10);container=frame;
            }
            foreach(var item in items)
            {
                int id=item.Id;bool reserved=S.UsedReserved(S.CurrentShop,id);
                string status=reserved?"顾客已拿取 · 等待收银":onShelf.Contains(id)?"已上架 · 等待挑选":item.Listed?"等待货架空位":item.Inspected?"已鉴定 · 待定价上架":"待鉴定";
                var card=Ui.Panel(container,"UsedStock_"+id,shelfOnly?ShelfBack:Ui.PanelBg2).rectTransform;Ui.VList(card,5,8);
                if(shelfOnly)
                {
                    var icon=Ui.Rect(card,"UsedIllustration_"+id);Ui.Size(icon,-1,85);
                    DrawUsedIcon(icon,item.Product);
                }
                Line(card,GameSession.UsedNames[item.Product]+" #"+id+" · "+status,17,Ui.Accent);
                Line(card,(item.Inspected?GameSession.ConditionName(item.Condition):"状态待鉴定")+" · "+GameSession.UsedWeight(item).ToString("0.##")+" 千克 · 成本 ¥"+item.Cost+" · 标价 ¥"+item.Price,15);
                var row=Ui.Row(card,34,5);
                Ui.Size(Ui.Button(row,item.Listed?"调整售价":"定价并上架",()=>OpenUsedPrice(id,owner),16,Ui.ButtonHot,item.Inspected&&!reserved),-1,34,1);
                if(item.Listed)Ui.Size(Ui.Button(row,"下架",()=>{S.UnlistUsedItem(id);owner?.Rebuild();RebuildDock();},16,Ui.ButtonBg,!reserved),85,34);
                if(shelfOnly)Ui.Size(Ui.Panel(container,"Plank",WoodLight),-1,10);
            }
            if(items.Length==0)Line(parent,"暂无二手转售库存。",16,Ui.Dim);
            if(!pricingOnly)BuildUsedSalesHistory(parent,state);
        }
        private static void DrawUsedIcon(RectTransform root,int product)
        {
            Color dark=new Color(.22f,.25f,.24f),gold=new Color(.8f,.66f,.38f);
            if(product==0){Chip(root,gold,.32f,.6f,.68f,.94f);Chip(root,dark,.48f,.18f,.52f,.65f);Chip(root,dark,.3f,.1f,.7f,.19f);}
            else if(product==1){Chip(root,dark,.34f,.08f,.66f,.93f);Chip(root,Color.gray,.4f,.45f,.6f,.82f);Chip(root,Color.black,.43f,.17f,.57f,.4f);}
            else if(product==2){Chip(root,new Color(.45f,.3f,.2f),.45f,.02f,.55f,.98f);Chip(root,gold,.35f,.3f,.65f,.72f);Chip(root,Color.white,.39f,.35f,.61f,.67f);Chip(root,dark,.49f,.47f,.51f,.62f);Chip(root,dark,.49f,.47f,.57f,.5f);}
            else {Chip(root,gold,.4f,.3f,.6f,.85f);Chip(root,new Color(.55f,.38f,.23f),.3f,.1f,.7f,.3f);}
        }
        private void OpenUsedPrice(int id,Window owner)
        {
            var item=S.SpecialBusiness(S.CurrentShop.Place.Id).Used.FirstOrDefault(i=>i.Id==id&&!i.Sold);if(item==null)return;
            long price=Math.Max(1,item.Price);
            OpenWindow("二手商品售价",600,330,w=>
            {
                Line(w.Content,GameSession.UsedNames[item.Product]+" · "+GameSession.ConditionName(item.Condition),20,Ui.Accent);
                Line(w.Content,"收购成本 ¥"+item.Cost+" · 建议售价 ¥"+GameSession.UsedValue(item),17);
                var row=Ui.Row(w.Content,40,6);
                Ui.Size(Ui.Button(row,"−",()=>{price=Math.Max(1,price-1);w.Rebuild();},18),55);
                var input=Ui.TextInput(row,price.ToString(),"售价",text=>{if(long.TryParse(text,out long value))price=Math.Max(1,Math.Min(1000000,value));});
                input.contentType=UnityEngine.UI.InputField.ContentType.IntegerNumber;Ui.Size(input,-1,40,1);
                Ui.Size(Ui.Button(row,"+",()=>{price=Math.Min(1000000,price+1);w.Rebuild();},18),55);
                Ui.Size(Ui.Button(w.Content, "确认售价并上架", () =>
                {
                    if (!TryListUsedItem(id, price)) return;
                    w.Close();
                    owner?.Rebuild();
                    RebuildDock();
                }, 18, Ui.ButtonHot), -1, 42);
            });
        }
    }
}
