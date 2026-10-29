using System;
using System.Collections.Generic;
using System.Text;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
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

        // ---------- 档口 ----------

        public bool StallSells(Place stall, PartDef part)
        {
            if (stall == null || stall.Kind != PlaceKind.Wholesaler) return false;
            foreach (PartCategory c in stall.Sells)
            {
                if (c == part.Category) return true;
            }
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
            if (stall == null || stall.Kind != PlaceKind.Wholesaler || CurrentPlace != stall) return BargainResult.NotAllowed;
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
            if (CurrentPlace != stall || !StallSells(stall, part)) return Fail("这家档口不卖这个。");
            if (!IsWholesalerOpen) return Fail("批发市场工作日 9:00–18:00 营业。");
            if (count <= 0) return false;
            int remaining = StallRemaining(stall, part);
            if (count > remaining)
                return Fail(remaining <= 0 ? $"老板说{part.Name}今天不能再多给你了，明天再来。" : $"老板说{part.Name}今天最多再给你 {remaining} 个。多来往，他会放心多给。");
            if (!Cargo.CanAdd(part, true, count))
                return Fail($"{CurrentVehicle.Name}装不下了（剩 {Cargo.FreeSpace:0.#} 格）。");
            Money total = StallPrice(stall, part) * count;
            if (!Ledger.TryDebit(Clock.Today, "进货", total, $"{stall.Name} {part.Name}×{count}"))
                return Fail($"要 {total}，钱不够。");
            Cargo.TryAdd(part, true, count);
            RecordStallPurchase(stall, part, count);
            AdvanceClock(2);
            return true;
        }

        /// <summary>把随身的货卸进店里，放不下的留在身上。</summary>
        public void UnloadCargo()
        {
            if (Cargo.IsEmpty) return;
            int moved = 0;
            bool leftover = false;
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
            if (moved > 0) Say($"卸货 {moved} 件进店。");
            if (leftover) Say("店里放不下了，剩下的货还在身上。拆掉包装能省地方。");
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

        // ---------- 1788 ----------

        public Money OnlinePrice(OnlineStore store, PartDef part) =>
            Online1788.PriceOf(store, Market.PriceOf(part)).Scale(1.0 - NpcPerks.OnlineDiscount(Relation(Npcs.Zhou)));

        /// <summary>在店里的电脑上下单，次日 10:00 送到店里。</summary>
        public bool PlaceOnlineOrder(string storeId, IReadOnlyList<(PartDef part, int count)> lines)
        {
            if (!InPlayerShop) return Fail("只能在店里的电脑上下单（按键机上不了 1788）。");
            if (!Online.IsDiscovered(storeId)) return false;
            OnlineStore store = Online1788.GetStore(storeId);

            Money total = Online1788.ShippingFee;
            double space = 0;
            var order = new OnlineOrder { Id = Online.NextOrderId, StoreId = storeId };
            foreach (var (part, count) in lines)
            {
                if (count <= 0) continue;
                total += OnlinePrice(store, part) * count;
                space += Stock.SpaceOf(part, true, count);
                order.Lines.Add(new OnlineOrderLine { PartId = part.Id, Count = count });
            }
            if (order.Lines.Count == 0) return Fail("购物车是空的。");
            double free = PlayerShop.Stock.FreeSpace - Online.PendingSpace();
            if (space > free + 1e-9) return Fail($"店里放不下（需要 {space:0.#} 格，只剩 {Math.Max(0, free):0.#} 格）。");
            if (!Ledger.TryDebit(Clock.Today, "进货", total, $"1788 {store.Name}")) return Fail($"要 {total}（含运费），钱不够。");

            GameDate next = Clock.Today.AddDays(1);
            order.DeliverAtMinute = GameTimestamp.From(next, Online1788.DeliveryMinuteOfDay).TotalMinutes;
            order.TotalCents = total.Cents;
            Online.NextOrderId++;
            Online.TotalOrders++;
            Online.AddPending(order);
            Say($"下单成功，{total}，明天 10:00 送到店里。");

            if (Online.TotalOrders == 2 && Online.Discover("s_flash"))
                Say("1788 首页给你推荐了一家新店：闪电优选。");
            AdvanceClock(5);
            return true;
        }

        private void DeliverOnlineOrders(GameTimestamp now)
        {
            if (Online.Pending.Count == 0) return;
            foreach (OnlineOrder o in Online.TakeDue(now))
            {
                OnlineStore store = Online1788.GetStore(o.StoreId);
                StoreRecord rec = Online.RecordOf(o.StoreId);
                int good = 0, bad = 0;
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
                            PlayerShop.Stock.ForceAdd(part, true, 1);
                            good++;
                        }
                    }
                }
                rec.ItemsReceived += good + bad;
                rec.Defects += bad;
                string body = $"{store.Name} 的货到了：{good} 件完好。";
                if (bad > 0) body += $"\n{bad} 件是次品，只能扔掉：{badNames}";
                Show("1788 到货", body);
            }
        }
    }
}
