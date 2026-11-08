using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Home;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Time;
using PCShopSell.Core.Trade;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    public enum BargainResult
    {
        NotAllowed,
        AlreadyTried,
        Success,
        Failed,
    }

    public sealed partial class GameSession
    {
        public const double StallBaseFactor = 0.86;

        /// <summary>档口 → (砍价的日期, 当天的折扣调整)。</summary>
        private readonly Dictionary<string, (int day, double bonus)> _bargains = new Dictionary<string, (int, double)>();
        private int? _stallCheckoutMinutes;

        // ---------- 档口 ----------

        public IEnumerable<Place> WholesaleStallsAt(Place market) =>
            City.OfKind(PlaceKind.Wholesaler).Where(stall => SharesWholesaleMarket(market, stall));

        private static bool SharesWholesaleMarket(Place a, Place b) =>
            a?.Kind == PlaceKind.Wholesaler && b?.Kind == PlaceKind.Wholesaler && a.NodeId == b.NodeId;

        public static string StallSupplierName(Place stall) =>
            stall.Id == Stalls.Cpu && Npcs.OwnerOf(stall.Id) is NpcDef owner ? owner.Name + "档口" : stall.Name;

        public bool StallSells(Place stall, PartDef part)
        {
            if (stall == null || stall.Kind != PlaceKind.Wholesaler || !IndustryCycles.Available(part,Clock.Today.TotalDays)) return false;
            foreach (PartCategory c in stall.Sells) if(c==part.Category)return true;
            // Existing specialist stalls also carry a small overlapping range, so the same
            // model has real alternative merchants without creating duplicate city buildings.
            if(stall.Id=="stall_cpu")return part.Category==PartCategory.Gpu||part.Category==PartCategory.PowerCase;
            if(stall.Id=="stall_gpu")return part.Category==PartCategory.Cpu||part.Category==PartCategory.Storage;
            if(stall.Id=="stall_mem")return part.Category==PartCategory.Motherboard||part.Category==PartCategory.Cpu;
            if(stall.Id=="stall_psu")return part.Category==PartCategory.Gpu||part.Category==PartCategory.Memory;
            return false;
        }

        /// <summary>档口今天的批发价。</summary>
        public Money StallPrice(Place stall, PartDef part)
        {
            double f = StallBaseFactor + StallVariation(stall.Id) - BargainBonus(stall.Id) - NpcPerks.StallDiscount(Relation(Npcs.Jie)) - StallOwnerDiscount(stall);
            return Money.FromYuan((long)Math.Round(Market.PriceOf(part).Yuan * f));
        }

        public bool HasBargainedToday(Place stall) =>
            _bargains.TryGetValue(stall.Id, out var b) && b.day == Clock.Today.TotalDays;

        /// <summary>和档口老板砍价：每家每天一次。成功了今天全场便宜 3%~7%，失败了老板不高兴、贵 1%。</summary>
        public BargainResult Bargain(Place stall)
        {
            if (!SharesWholesaleMarket(CurrentPlace, stall)) return BargainResult.NotAllowed;
            if (HasBargainedToday(stall)) return BargainResult.AlreadyTried;
            double chance = 0.35 + EffectiveCharm * 0.01 + (Player.Tired ? -0.15 : 0);
            AdvanceClock(5);
            bool ok = Random.Chance(Math.Max(0.05, Math.Min(0.9, chance)));
            double bonus = ok ? Random.Range(0.03, 0.07) : -0.01;
            _bargains[stall.Id] = (Clock.Today.TotalDays, bonus);
            if (ok) Player.Charm = Math.Min(100, Player.Charm + 1);
            return ok ? BargainResult.Success : BargainResult.Failed;
        }

        /// <summary>在档口进货（盒装），货放到随身载具上。</summary>
        public bool BuyAtStall(Place stall, PartDef part, int count)
        {
            if (!SharesWholesaleMarket(CurrentPlace, stall) || !StallSells(stall, part)) return Fail("这家档口不卖这个。");
            if (!IsWholesalerOpen) return Fail($"批发市场工作日 {Hm(WholesalerOpenMinute)}–{Hm(WholesalerCloseMinute)} 营业。");
            if (count <= 0) return false;
            int remaining = StallRemaining(stall, part);
            if (count > remaining)
                return Fail(remaining <= 0 ? $"老板说{part.Name}今天不能再多给你了，明天再来。" : $"老板说{part.Name}今天最多再给你 {remaining} 个。这个型号的当日实货已不足。");
            Money total = StallPrice(stall, part) * count;
            if (SendByCourier)
            {
                if (!PayAndSend("进货", total, $"{stall.Name} {part.Name}×{count}", true, new CourierItem { Kind = CourierItemKind.Part, Id = part.Id, Count = count, Boxed = true }))
                    return false;
            }
            else
            {
                if (!Cargo.CanAdd(part, true, count))
                    return Fail($"拿不动了：{CurrentVehicle.Name}最多 {CarryLimitKg:0.##} 千克，现在已经 {CarriedKg:0.##} 千克（还能装 {Cargo.FreeSpace * Backpack.KgPerSpace:0.##} 千克的货）。可以改成“叫车送到店里”。");
                if (!Ledger.TryDebit(Clock.Today, "进货", total, $"{stall.Name} {part.Name}×{count}"))
                    return Fail($"要 {total}，钱不够。");
                Cargo.TryAdd(part, true, count);
            }
            RecordStallPurchase(stall, part, count);
            var purchaseOwner = Npcs.OwnerOf(stall.Id);
            if (purchaseOwner != null) RememberSocial(purchaseOwner.Id, $"在{stall.Name}购入{part.Name} ×{count}。");
            if (_stallCheckoutMinutes.HasValue) _stallCheckoutMinutes += 2;
            else AdvanceClock(2);
            return true;
        }

        /// <summary>把随身的货卸进店里，放不下的留在身上。</summary>
        public void UnloadCargo()
        {
            int moved = MoveCargoToShop(out bool leftover);
            if (moved > 0) Say($"卸货 {moved} 件进店。");
            if (leftover) Say("店里放不下了，剩下的货还在身上。拆掉包装能省地方。");
        }

        private int MoveCargoToShop(out bool leftover)
        {
            int moved = 0;
            leftover = false;
            foreach (StockLine l in Cargo.Lines())
            {
                int n = Math.Min(l.Count, PlayerShop.Stock.MaxAddable(l.Part, l.Boxed));
                if (n > 0)
                {
                    Cargo.TryRemove(l.Part, l.Boxed, n);
                    PlayerShop.Stock.TryAdd(l.Part, l.Boxed, n);
                    moved += n;
                }
                if (n < l.Count) leftover = true;
            }
            return moved;
        }

        private double BargainBonus(string stallId) =>
            _bargains.TryGetValue(stallId, out var b) && b.day == Clock.Today.TotalDays ? b.bonus : 0;

        /// <summary>每家档口固定的价格差异（-2%~+2%），由 ID 决定。</summary>
        private static double StallVariation(string id)
        {
            uint h = 2166136261;
            foreach (char c in id) h = (h ^ c) * 16777619;
            return ((h % 1000) / 1000.0 - 0.5) * 0.04;
        }

        // ---------- 网购 ----------

        public Money OnlinePrice(OnlineStore store, PartDef part) =>
            Online1788.PriceOf(store, Market.PriceOf(part)).Scale(1.0 - NpcPerks.OnlineDiscount(Relation(Npcs.Zhou)));

        /// <summary>用手机上的 网购 app 下单（人在哪都行），次日 10:00 送到店里或家里（toHome）。</summary>
        public bool PlaceOnlineOrder(string storeId, IReadOnlyList<(PartDef part, int count)> lines, bool toHome = false) =>
            PlaceOnlineOrderCore(storeId, lines, true, toHome);

        public bool PlaceOnlineOrder(string storeId, IReadOnlyList<(PartDef part, int count)> lines, OwnedShop destination)
        {
            if (destination != null && ShopAt(destination.Place.Id) != destination) return Fail("所选收货门店已不可用。");
            return PlaceOnlineOrderCore(storeId, lines, true, destination == null, destination);
        }

        /// <summary>manual = 玩家亲自在手机上挑货下单（花 5 分钟）；自动补货是 app 定时下单。</summary>
        private bool PlaceOnlineOrderCore(string storeId, IReadOnlyList<(PartDef part, int count)> lines, bool manual,
            bool toHome = false, OwnedShop destination = null)
        {
            string appBlock = AppBlocker(PhoneApps.Shop);
            if (appBlock != null) return Fail(appBlock);
            destination = toHome ? null : destination ?? _focus;
            if (!toHome && destination?.Rented != true) return Fail("请选择可用的收货门店，或选“送到家里”。");
            // Old callers also use the single shopping channel for new orders.
            OnlineStore store = Online1788.Shopping;
            storeId = store.Id;

            Money total = Online1788.ShippingFee;
            double space = 0;
            var order = new OnlineOrder { Id = Online.NextOrderId, StoreId = storeId, ToHome = toHome, ShopId = destination?.Place.Id };
            foreach (var group in lines.Where(l => l.part != null && l.count > 0).GroupBy(l => l.part.Id))
            {
                var part = group.First().part; int count = group.Sum(l => l.count);
                if(!IndustryCycles.Available(part,Clock.Today.TotalDays))return Fail("该代配件尚未发布。");
                if (count > 1000) return Fail("同一商品每单最多1000件。");
                total += OnlinePrice(store, part) * count;
                space += Stock.SpaceOf(part, true, count);
                order.Lines.Add(new OnlineOrderLine { PartId = part.Id, Count = count });
            }
            if (order.Lines.Count == 0) return Fail("购物车是空的。");
            double free = double.PositiveInfinity;
            if (destination != null) WithShop(destination, () => free = PlayerShop.Stock.FreeSpace - IncomingShopSpace);
            if (space > free + 1e-9) return Fail($"店里放不下（需要 {space:0.#} 格，只剩 {Math.Max(0, free):0.#} 格）。");
            if (!PayUnifiedOnlineOrder(order, total, store)) return Fail($"要 {total}（含运费），钱不够。");

            GameDate next = Clock.Today.AddDays(1);
            order.DeliverAtMinute = GameTimestamp.From(next, Online1788.DeliveryMinuteOfDay).TotalMinutes;
            order.TotalCents = total.Cents;
            Online.NextOrderId++;
            Online.TotalOrders++;
            Online.AddPending(order);
            RegisterProcurementOrder("online-order:" + order.Id, "网购", order.ShopId, store.Id, total.Cents, order.DeliverAtMinute,
                order.Lines.Select(l => new PurchaseLine { Kind = PurchaseGoodsKind.Part, PartId = l.PartId, Count = l.Count, SourceId = store.Id }), order.Id, null);
            string where = CourierDestName(destination);
            Say(manual ? $"下单成功，{total}，明天 10:00 送到{where}。" : $"自动补货下单：{store.Name}，{total}，明天 10:00 送到店里{ShopTag}。");

            if (manual)
            {
                RememberPurchaseDelivery(destination);
                AdvanceClock(5);
            }
            return true;
        }

        private OwnedShop OnlineOrderDestination(OnlineOrder order) => PurchaseDeliveryShop(!order.ToHome, order.ShopId);

        private void DeliverOnlineOrders(GameTimestamp now)
        {
            if (Online.Pending.Count == 0) return;
            foreach (OnlineOrder o in Online.TakeDue(now))
            {
                OnlineStore store = Online1788.GetStore(o.StoreId);
                StoreRecord rec = Online.RecordOf(o.StoreId);
                OwnedShop dest = OnlineOrderDestination(o);
                int good = 0, bad = 0, toTemp = 0;
                var badNames = new StringBuilder();
                foreach (OnlineOrderLine l in o.Lines)
                {
                    PartDef part = PartCatalog.Get(l.PartId);
                    for (int i = 0; i < l.Count; i++)
                    {
                        if (Random.Chance(store.DefectRate))
                        {
                            bad++;
                            if (badNames.Length > 0) badNames.Append("、");
                            badNames.Append(part.Name);
                        }
                        else
                        {
                            if (dest == null) toTemp += StorePartAtHome(part, true, 1);
                            else dest.Sim.Stock.ForceAdd(part, true, 1);
                            good++;
                        }
                    }
                }
                rec.ItemsReceived += good + bad;
                rec.Defects += bad;
                string delivery = dest != null ? $"已放入{CourierDestName(dest)}后仓"
                    : o.ToHome ? "已放入家中后仓" : "原门店已停用，已转送家中后仓";
                string body = $"{store.Name} 的货到了（{delivery}）：{good} 件完好。";
                if (toTemp > 0) body += $"\n家里纸箱满了，{toTemp} 件放在临时存放，能放 {TempKeepDays} 天，到期前回家打开纸箱清掉。";
                if (bad > 0) body += $"\n{bad} 件是次品，只能扔掉：{badNames}";
                Show("网购 到货", body);
            }
        }
    }
}
