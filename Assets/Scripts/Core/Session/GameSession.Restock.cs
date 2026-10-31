using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Shop;
using PCShopSell.Core.Trade;

namespace PCShopSell.Core.Session
{
    /// <summary>缺货提醒、自动补货（每天 9:00 用手机在 1788 下单）。</summary>
    public sealed partial class GameSession
    {
        public const int RestockCheckMinute = 9 * 60;
        public const int RestockMaxTarget = 30;

        /// <summary>库存（含在途）降到目标的 40% 以下才补。</summary>
        public const double RestockTriggerRatio = 0.4;

        /// <summary>自动补货之后至少还要剩这么多现金，免得把房租钱花光。</summary>
        public static readonly Money RestockCashReserve = Money.FromYuan(500);

        public long RestockReserveYuan { get; set; } = 500;
        public int RestockReserveDays { get; set; } = 7;
        public long RestockDailyBudgetYuan { get; set; }
        public Money EffectiveRestockReserve => Money.FromYuan(Math.Max(Math.Max(500, RestockReserveYuan),
            (long)Math.Ceiling((TotalShopRent.Yuan / 30.0 + CurrentHomeRent.Yuan / 30.0 + Employees.Sum(x => x.Person.DailyWageYuan) + McnDailyWages + VehicleCount(VehicleKind.Car) * 40 + VehicleCount(VehicleKind.Van) * 60) * Math.Max(0, RestockReserveDays)))) + UpcomingBusinessPayments(RestockReserveDays);

        private Dictionary<string, int> _restock => _focus.Restock;
        private readonly Dictionary<PartCategory, int> _noStockAlertDay = new Dictionary<PartCategory, int>();

        /// <summary>自动补货下单的店；null = 自动挑最便宜的（会避开你亲眼见过次品很多的店）。</summary>
        public string RestockStoreId { get; set; }

        /// <summary>最近一次自动补货的结果（给界面看）。</summary>
        public string LastRestockNote { get; private set; }

        public IEnumerable<KeyValuePair<string, int>> RestockTargets => _restock.Where(kv => kv.Value > 0);

        public int RestockTarget(string partId) => _restock.TryGetValue(partId, out int n) ? n : 0;

        public void SetRestockTarget(string partId, int target)
        {
            if (PartCatalog.Get(partId) == null) return;
            target = Math.Max(0, Math.Min(RestockMaxTarget, target));
            if (target == 0) _restock.Remove(partId);
            else _restock[partId] = target;
        }

        /// <summary>这件配件在路上（已下单未到货）的数量。</summary>
        public int PendingOnlineCount(string partId)
        {
            int n = 0;
            foreach (OnlineOrder o in Online.Pending)
                if (!o.ToHome && DeliveryShop(o.ShopId) == _focus)   // 送家里的不算店里的库存
                    foreach (OnlineOrderLine l in o.Lines)
                        if (l.PartId == partId) n += l.Count;
            foreach (var r in Couriers) if (HeadsToThisShop(r))
                foreach (var item in r.Items)
                    if (item.Kind == CourierItemKind.Part && item.Id == partId) n += item.Count;
            return n + DirectShipments.Where(x => x.PartId == partId && DeliveryShop(x.ShopId) == _focus).Sum(x => x.Count)
                + _employeeTrips.Where(t => !t.Delivered && DeliveryShop(t.ShopId) == _focus).Sum(t => t.Items.Where(l => l.PartId == partId).Sum(l => l.Count));
        }

        /// <summary>店里现有 + 在路上。</summary>
        public int StockAndPending(PartDef p) => PlayerShop.Stock.CountAll(p) + PendingOnlineCount(p.Id);

        private void OnShopCustomerLeft(ShopCustomer c)
        {
            if (c.Reason != LeaveReason.NoStock) return;
            int today = Clock.Today.TotalDays;
            if (_noStockAlertDay.TryGetValue(c.DesiredCategory, out int d) && d == today) return;
            _noStockAlertDay[c.DesiredCategory] = today;
            string reason = ShelvesOfKind(ShelfKinds.ForCategory(c.DesiredCategory).Id) == 0 ? "没有设置对应货架"
                : PartCatalog.All.Any(p => p.Category == c.DesiredCategory && PlayerShop.Stock.CountAll(p) > 0) ? "货架容量或型号不能满足需求"
                : PartCatalog.All.Any(p => p.Category == c.DesiredCategory && PendingOnlineCount(p.Id) > 0) ? "货物还在配送中" : "库存售罄";
            Say($"有顾客想买{c.DesiredPart.Name}，但{reason}，空手走了。");
        }

        /// <summary>自动补货用哪家店：指定的，或者已发现的里最便宜的（排除亲眼见过次品多的）。</summary>
        public OnlineStore PickRestockStore(IReadOnlyList<PartDef> parts)
        {
            if (RestockStoreId != null && Online.IsDiscovered(RestockStoreId)) return Online1788.GetStore(RestockStoreId);
            OnlineStore best = null;
            long bestCost = long.MaxValue;
            foreach (OnlineStore st in Online1788.Stores)
            {
                if (!Online.IsDiscovered(st.Id)) continue;
                StoreRecord rec = Online.RecordOf(st.Id);
                if (rec.ItemsReceived >= 4 && rec.Defects * 5 > rec.ItemsReceived) continue;
                long cost = 0;
                foreach (PartDef p in parts) cost += OnlinePrice(st, p).Cents;
                if (cost < bestCost)
                {
                    bestCost = cost;
                    best = st;
                }
            }
            return best;
        }

        /// <summary>算出现在该补哪些货、补多少（按缺得多不多排序）。</summary>
        public List<(PartDef part, int count)> RestockNeeds()
        {
            var needs = new List<(PartDef part, int count, double ratio)>();
            foreach (var kv in _restock)
            {
                PartDef p = PartCatalog.Get(kv.Key);
                if (p == null || kv.Value <= 0) continue;
                int have = StockAndPending(p);
                if (have * 10 > kv.Value * 4) continue;
                needs.Add((p, kv.Value - have, (double)have / kv.Value));
            }
            needs.Sort((a, b) => a.ratio.CompareTo(b.ratio));
            return needs.Select(n => (n.part, n.count)).ToList();
        }

        /// <summary>每天 9:00：库存（含在途）低于目标的 40% 就用手机在 1788 补到目标，次日 10:00 到。</summary>
        public void RunAutoRestock()
        {
            foreach (OwnedShop s in _shops.ToList())
                if (s.Rented) WithShop(s, RunAutoRestockHere);
        }

        private void RunAutoRestockHere()
        {
            if (_restock.Count == 0 || IsGameOver || !HasShop) return;
            List<(PartDef part, int count)> needs = RestockNeeds();
            if (needs.Count == 0) return;

            string block = AppBlocker(PhoneApps.Wholesale);
            if (block != null) { Note("自动补货没执行：" + block); return; }

            OnlineStore store = PickRestockStore(needs.Select(n => n.part).ToList());
            if (store == null) { Note("自动补货没执行：没有能下单的店。"); return; }

            // 先按缺得最厉害的排，一个个往里放，放不下仓位的就少补
            double free = PlayerShop.Stock.FreeSpace - IncomingShopSpace;
            long budget = Math.Max(0, Ledger.Balance.Cents - EffectiveRestockReserve.Cents - Online1788.ShippingFee.Cents);
            if (RestockDailyBudgetYuan > 0) budget = Math.Min(budget, Math.Max(0, RestockDailyBudgetYuan * 100 - Online1788.ShippingFee.Cents));
            var lines = new List<(PartDef part, int count)>();
            foreach (var (part, count) in needs)
            {
                double each = PCShopSell.Core.Inventory.Stock.SpaceOf(part, true, 1);
                int fit = each <= 0 ? count : (int)Math.Floor((free + 1e-9) / each);
                long price = Math.Max(1, OnlinePrice(store, part).Cents);
                int n = (int)Math.Min(Math.Min(count, fit), budget / price);
                if (n <= 0) continue;
                free -= n * each;
                budget -= n * price;
                lines.Add((part, n));
            }
            if (lines.Count == 0) { Note("自动补货没执行：可用预算或仓位不足，检查现金保留额和库存。"); return; }

            Money total = Online1788.ShippingFee;
            foreach (var (part, count) in lines) total += OnlinePrice(store, part) * count;
            if (!Ledger.CanAfford(total + EffectiveRestockReserve)) { Note($"自动补货没执行：要 {total}，扣掉后现金会低于 {EffectiveRestockReserve}，先不补。"); return; }

            if (PlaceOnlineOrderCore(store.Id, lines, false))
                LastRestockNote = $"{Clock.Today} 自动补货{ShopTag}：{store.Name}，{lines.Sum(l => l.count)} 件，{total}。";
        }

        private void Note(string message)
        {
            LastRestockNote = $"{Clock.Today} {message}";
            Say(message);
        }

        private void SaveRestock(SaveData d)
        {
            d.partMarkupIds = PlayerShop.PartMarkups.Keys.ToArray();
            d.partMarkupValues = PlayerShop.PartMarkups.Values.ToArray();
            d.restockIds = _restock.Keys.ToArray();
            d.restockTargets = _restock.Values.ToArray();
            d.restockStore = RestockStoreId;
            d.hasRestockPolicy = true;
            d.restockReserveYuan = RestockReserveYuan;
            d.restockReserveDays = RestockReserveDays;
            d.restockDailyBudgetYuan = RestockDailyBudgetYuan;
        }

        private void RestoreRestock(SaveData d)
        {
            string[] ids = d.partMarkupIds ?? Array.Empty<string>();
            double[] vals = d.partMarkupValues ?? Array.Empty<double>();
            var list = new List<KeyValuePair<string, double>>();
            for (int i = 0; i < ids.Length && i < vals.Length; i++)
                if (ids[i].StartsWith("g:") ? Gifts.Get(ids[i].Substring(2)) != null : PartCatalog.Exists(ids[i])) list.Add(new KeyValuePair<string, double>(ids[i], vals[i]));
            PlayerShop.RestorePartMarkups(list);

            _restock.Clear();
            string[] rids = d.restockIds ?? Array.Empty<string>();
            int[] rt = d.restockTargets ?? Array.Empty<int>();
            for (int i = 0; i < rids.Length && i < rt.Length; i++)
                if (PartCatalog.Get(rids[i]) != null && rt[i] > 0) _restock[rids[i]] = Math.Min(RestockMaxTarget, rt[i]);
            RestockStoreId = d.restockStore;
            if (d.hasRestockPolicy) { RestockReserveYuan = Math.Max(500, d.restockReserveYuan); RestockReserveDays = Math.Max(0, Math.Min(30, d.restockReserveDays)); RestockDailyBudgetYuan = Math.Max(0, d.restockDailyBudgetYuan); }
        }
    }
}
