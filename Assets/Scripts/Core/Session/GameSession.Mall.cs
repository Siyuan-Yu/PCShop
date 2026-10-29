using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;

namespace PCShopSell.Core.Session
{
    /// <summary>家具城买回去怎么运：全自己带走 / 全叫货车 / 能装的装走其余叫货车 / 能装的装走其余寄存在店里 / 全寄存在店里。</summary>
    public enum MallCarry
    {
        Self,
        Truck,
        Split,
        SplitStay,
        Stay,
    }

    public sealed class MallCartLine
    {
        public string Id;
        public int Count;
    }

    /// <summary>一次家具城结算的方案：多少钱、自己带多少、叫几辆货车。</summary>
    public sealed class MallPlan
    {
        public Money Goods;
        public Money TruckFees;
        public int Trucks;
        public double SelfKg;
        public double TruckKg;
        public double StashKg;
        public readonly List<MallCartLine> SelfItems = new List<MallCartLine>();
        public readonly List<MallCartLine> TruckItems = new List<MallCartLine>();

        /// <summary>寄存在店里、以后自己来搬的。</summary>
        public readonly List<MallCartLine> StashItems = new List<MallCartLine>();

        /// <summary>这个方案走不通的原因；能走则为 null。</summary>
        public string Blocker;

        public Money Total => Goods + TruckFees;
        public int SelfCount => SelfItems.Sum(l => l.Count);
        public int TruckCount => TruckItems.Sum(l => l.Count);
        public int StashCount => StashItems.Sum(l => l.Count);
    }

    /// <summary>
    /// 家具城：东西随便挑进购物车，最后一起结算；结算时再决定怎么运。
    /// 能装多少看来时的出行方式（载具的格子，1 格 = 2 公斤，和背包、进货的配件共用）；
    /// 装不下的可以叫货车（每辆 ¥120、最多装 200 公斤，买多了要多辆，早早送到家），也可以先寄存在店里，改天自己来搬（可以分好几趟，不过期）。
    /// </summary>
    public sealed partial class GameSession
    {
        /// <summary>一辆货车最多装多少公斤。</summary>
        public const double TruckCapacityKg = 200;

        private readonly List<MallCartLine> _mallCart = new List<MallCartLine>();

        public IReadOnlyList<MallCartLine> MallCart => _mallCart;

        public bool AtMall => CurrentPlace != null && CurrentPlace.Business == MallBusiness;

        public int MallCartCount => _mallCart.Sum(l => l.Count);

        public int MallCartOf(string id) => _mallCart.FirstOrDefault(l => l.Id == id)?.Count ?? 0;

        public Money MallCartGoods => TotalGoods(_mallCart);

        public double MallCartKg => TotalKg(_mallCart);

        private static Money TotalGoods(IEnumerable<MallCartLine> lines)
        {
            Money total = Money.Zero;
            foreach (MallCartLine l in lines) total += FurnitureCatalog.Get(l.Id).Price * l.Count;
            return total;
        }

        private static double TotalKg(IEnumerable<MallCartLine> lines) => lines.Sum(l => FurnitureCatalog.Get(l.Id).WeightKg * l.Count);

        public bool MallCartAdd(string id, int count = 1)
        {
            FurnitureDef d = FurnitureCatalog.Get(id);
            if (d == null || d.Starter || count <= 0) return false;
            if (!AtMall) return Fail("要去家具城才能买。");
            MallCartLine line = _mallCart.FirstOrDefault(l => l.Id == id);
            if (line == null) _mallCart.Add(new MallCartLine { Id = id, Count = count });
            else line.Count += count;
            return true;
        }

        public bool MallCartRemove(string id, int count = 1)
        {
            MallCartLine line = _mallCart.FirstOrDefault(l => l.Id == id);
            if (line == null || count <= 0) return false;
            line.Count -= count;
            if (line.Count <= 0) _mallCart.Remove(line);
            return true;
        }

        public void MallCartClear() => _mallCart.Clear();

        /// <summary>把这些东西装货车，最少要几辆（每辆最多 <see cref="TruckCapacityKg"/> 公斤，一件不拆）。</summary>
        public static int TrucksNeeded(IEnumerable<MallCartLine> lines)
        {
            var weights = new List<double>();
            foreach (MallCartLine l in lines)
            {
                double w = FurnitureCatalog.Get(l.Id).WeightKg;
                for (int i = 0; i < l.Count; i++) weights.Add(w);
            }
            weights.Sort((a, b) => b.CompareTo(a));
            var free = new List<double>();
            foreach (double w in weights)
            {
                int bin = free.FindIndex(f => f + 1e-9 >= w);
                if (bin < 0) free.Add(TruckCapacityKg - w);
                else free[bin] -= w;
            }
            return free.Count;
        }

        private static void AddTo(List<MallCartLine> list, string id)
        {
            MallCartLine l = list.FirstOrDefault(x => x.Id == id);
            if (l == null) list.Add(new MallCartLine { Id = id, Count = 1 });
            else l.Count++;
        }

        /// <summary>按某种运法算一下购物车：多少钱、自己带哪些、哪些叫货车、走不走得通。</summary>
        public MallPlan PlanMallCheckout(MallCarry mode) => PlanLines(_mallCart, mode);

        private MallPlan PlanLines(IReadOnlyList<MallCartLine> lines, MallCarry mode)
        {
            var plan = new MallPlan { Goods = TotalGoods(lines) };
            if (lines.Count == 0 || lines.All(l => l.Count <= 0))
            {
                plan.Blocker = "购物车是空的";
                return plan;
            }

            // 一件一件摊开：轻的排前面，自己带走时先装轻的
            var units = new List<string>();
            foreach (MallCartLine l in lines)
                for (int i = 0; i < l.Count; i++) units.Add(l.Id);
            units = units.OrderBy(id => FurnitureCatalog.Get(id).WeightKg).ToList();

            double free = FreeKg;
            double usedSelf = 0;
            bool spilled = false;
            bool fitsFirst = mode == MallCarry.Split || mode == MallCarry.SplitStay;
            foreach (string id in units)
            {
                double w = FurnitureCatalog.Get(id).WeightKg;
                if (mode == MallCarry.Self
                    || (fitsFirst && !spilled && usedSelf + w <= free + 1e-9))
                {
                    usedSelf += w;
                    AddTo(plan.SelfItems, id);
                    continue;
                }
                spilled = true;
                if (mode == MallCarry.Stay || mode == MallCarry.SplitStay) AddTo(plan.StashItems, id);
                else AddTo(plan.TruckItems, id);
            }
            plan.SelfKg = TotalKg(plan.SelfItems);
            plan.TruckKg = TotalKg(plan.TruckItems);
            plan.StashKg = TotalKg(plan.StashItems);

            plan.Trucks = plan.TruckItems.Count == 0 ? 0 : TrucksNeeded(plan.TruckItems);
            plan.TruckFees = FurnitureCatalog.TruckFee * plan.Trucks;

            if (mode == MallCarry.Self && plan.SelfKg > free + 1e-9)
                plan.Blocker = $"共 {plan.SelfKg:0.#} 公斤，{CurrentVehicle.Name}现在只能再带 {free:0.#} 公斤";
            else if (mode == MallCarry.Split && (plan.SelfItems.Count == 0 || plan.TruckItems.Count == 0))
                plan.Blocker = plan.SelfItems.Count == 0 ? "身上一件也带不了，全叫货车吧" : "都带得走，不用叫货车";
            else if (mode == MallCarry.SplitStay && (plan.SelfItems.Count == 0 || plan.StashItems.Count == 0))
                plan.Blocker = plan.SelfItems.Count == 0 ? "身上一件也带不了，全寄存在店里吧" : "都带得走，不用寄存";
            return plan;
        }

        // ---------- 寄存在店里的货 ----------

        private readonly List<MallCartLine> _mallStash = new List<MallCartLine>();

        /// <summary>已经付过钱、寄存在家具城的货（不过期，随时来搬，可以分几趟）。</summary>
        public IReadOnlyList<MallCartLine> MallStash => _mallStash;

        public int MallStashCount => _mallStash.Sum(l => l.Count);

        public double MallStashKg => TotalKg(_mallStash);

        private void AddStash(string id, int count)
        {
            MallCartLine l = _mallStash.FirstOrDefault(x => x.Id == id);
            if (l == null) _mallStash.Add(new MallCartLine { Id = id, Count = count });
            else l.Count += count;
        }

        private void RemoveStash(string id, int count)
        {
            MallCartLine l = _mallStash.FirstOrDefault(x => x.Id == id);
            if (l == null) return;
            l.Count -= count;
            if (l.Count <= 0) _mallStash.Remove(l);
        }

        /// <summary>从寄存的货里搬走一些放进背包 / 载具（要看还剩多少格）。</summary>
        public bool TakeFromStash(string id, int count = 1)
        {
            if (!AtMall) return Fail("要去家具城才能搬自己寄存的货。");
            MallCartLine l = _mallStash.FirstOrDefault(x => x.Id == id);
            if (l == null || count <= 0 || l.Count < count) return Fail("寄存的货里没有这么多。");
            double kg = FurnitureCatalog.Get(id).WeightKg * count;
            if (kg > FreeKg + 1e-9)
                return Fail($"装不下了：这些要 {kg:0.#} 公斤，{CurrentVehicle.Name}还剩 {FreeKg:0.#} 公斤。可以叫货车，或者改天再来搬。");
            RemoveStash(id, count);
            Backpack.AddFurniture(id, count);
            ApplyCargoLimit();
            Say($"从店里搬了「{FurnitureCatalog.Get(id).Name}」{(count > 1 ? "×" + count + " " : "")}。");
            return true;
        }

        /// <summary>能搬多少搬多少（先搬轻的），返回搬走几件。</summary>
        public int TakeStashWhatFits()
        {
            if (!AtMall) { Fail("要去家具城才能搬自己寄存的货。"); return 0; }
            int taken = 0;
            foreach (string id in _mallStash.SelectMany(l => Enumerable.Repeat(l.Id, l.Count)).OrderBy(i => FurnitureCatalog.Get(i).WeightKg).ToList())
            {
                if (FurnitureCatalog.Get(id).WeightKg > FreeKg + 1e-9) break;
                RemoveStash(id, 1);
                Backpack.AddFurniture(id, 1);
                ApplyCargoLimit();
                taken++;
            }
            if (taken > 0) Say($"从店里搬了 {taken} 件，还剩 {MallStashCount} 件寄存着。");
            else Fail($"一件也装不下了（{CurrentVehicle.Name}还剩 {FreeKg:0.#} 公斤）。");
            return taken;
        }

        public const string DeliverToShopFlag = "deliver_to_shop";

        /// <summary>货车送货地址：true = 我的店，false = 家。</summary>
        public bool DeliverToShop => Flags.Contains(DeliverToShopFlag);

        public string DeliveryName => DeliverToShop ? "店里" : "家";

        public void ToggleDeliveryAddress()
        {
            if (DeliverToShop) Flags.Remove(DeliverToShopFlag); else Flags.Add(DeliverToShopFlag);
            Say("货车以后送到：" + DeliveryName + "。");
        }

        /// <summary>把寄存的货全部叫货车拉回家要几辆。</summary>
        public int StashTrucksNeeded => _mallStash.Count == 0 ? 0 : TrucksNeeded(_mallStash);

        public Money StashTruckFee => FurnitureCatalog.TruckFee * StashTrucksNeeded;

        /// <summary>寄存的货全部叫货车，早早送到{DeliveryName}（运费另付）。</summary>
        public bool CallTruckForStash()
        {
            if (!AtMall) return Fail("要去家具城才能叫货车拉自己寄存的货。");
            if (_mallStash.Count == 0) return Fail("店里没有寄存的货。");
            int trucks = StashTrucksNeeded;
            Money fee = StashTruckFee;
            if (!Ledger.TryDebit(Clock.Today, "家具", fee, $"货车运费（寄存的货，{trucks} 辆）")) return Fail("钱不够。");
            foreach (MallCartLine l in _mallStash)
                _parcels.Add(new Parcel { Kind = ParcelKind.Furniture, Id = l.Id, Count = l.Count, DueDay = Clock.Today.TotalDays + 1, ToShop = DeliverToShop });
            int n = MallStashCount;
            _mallStash.Clear();
            Say($"叫了 {trucks} 辆货车（{fee}），把寄存的 {n} 件早早送到{DeliveryName}。");
            return true;
        }

        /// <summary>结算购物车：付钱，自己带的放进背包，货车拉的早早送到家，清空购物车。</summary>
        public bool CheckoutMall(MallCarry mode)
        {
            if (!AtMall) return Fail("要去家具城才能买。");
            return CheckoutLines(_mallCart.Select(l => new MallCartLine { Id = l.Id, Count = l.Count }).ToList(), mode, true);
        }

        private bool CheckoutLines(List<MallCartLine> lines, MallCarry mode, bool clearCart)
        {
            MallPlan plan = PlanLines(lines, mode);
            if (plan.Blocker != null) return Fail(plan.Blocker + "。");
            string what = string.Join("、", lines.Select(l => FurnitureCatalog.Get(l.Id).Name + (l.Count > 1 ? "×" + l.Count : "")));
            if (!Ledger.TryDebit(Clock.Today, "家具", plan.Total, what + (plan.Trucks > 0 ? $"（含货车运费 {plan.Trucks} 辆）" : ""))) return Fail("钱不够。");

            foreach (MallCartLine l in plan.SelfItems) Backpack.AddFurniture(l.Id, l.Count);
            if (plan.SelfItems.Count > 0) ApplyCargoLimit();
            foreach (MallCartLine l in plan.TruckItems)
                _parcels.Add(new Parcel { Kind = ParcelKind.Furniture, Id = l.Id, Count = l.Count, DueDay = Clock.Today.TotalDays + 1, ToShop = DeliverToShop });
            foreach (MallCartLine l in plan.StashItems) AddStash(l.Id, l.Count);
            if (clearCart) _mallCart.Clear();

            var parts = new List<string>();
            if (plan.SelfCount > 0) parts.Add($"{plan.SelfCount} 件自己带走（{plan.SelfKg:0.#} 公斤，回家或回店里自动放进家具背包）");
            if (plan.TruckCount > 0) parts.Add($"{plan.TruckCount} 件叫了 {plan.Trucks} 辆货车（{plan.TruckFees}），早早送到{DeliveryName}");
            if (plan.StashCount > 0) parts.Add($"{plan.StashCount} 件寄存在店里，改天自己来搬");
            Say($"买了 {what}。" + string.Join("；", parts) + "。");
            return true;
        }

        /// <summary>
        /// 家具城买一件：和农贸市场一样，按 <see cref="SendByCourier"/> 选装进背包还是叫车送到家。
        /// 背包装不下就只能叫车；一辆面包车装满了会再叫一辆。
        /// </summary>
        public bool BuyFurniture(string id)
        {
            FurnitureDef d = FurnitureCatalog.Get(id);
            if (d == null || d.Starter) return false;
            if (!AtMall) return Fail("要去家具城才能买。");
            if (SendByCourier)
                return PayAndSend("家具", d.Price, "「" + d.Name + "」", false, new CourierItem { Kind = CourierItemKind.Furniture, Id = id, Count = 1 });
            if (d.WeightKg > FreeKg + 1e-9)
                return Fail($"「{d.Name}」{d.WeightKg:0.#} 公斤，{CurrentVehicle.Name}只剩 {FreeKg:0.#} 公斤，装不下。切到“叫车送到家”吧。");
            return BuyFurnitureAtMall(id);
        }

        /// <summary>直接买一件自己带走（不走购物车）。背不动就买不了。</summary>
        public bool BuyFurnitureAtMall(string id)
        {
            FurnitureDef d = FurnitureCatalog.Get(id);
            if (d == null || d.Starter) return false;
            if (!AtMall) return Fail("要去家具城才能买。");
            return CheckoutLines(new List<MallCartLine> { new MallCartLine { Id = id, Count = 1 } }, MallCarry.Self, false);
        }

        /// <summary>直接买一件叫货车拉（运费 ¥120，早早送到家）。</summary>
        public bool BuyFurnitureByTruck(string id)
        {
            FurnitureDef d = FurnitureCatalog.Get(id);
            if (d == null || d.Starter) return false;
            if (!AtMall) return Fail("要去家具城才能买。");
            return CheckoutLines(new List<MallCartLine> { new MallCartLine { Id = id, Count = 1 } }, MallCarry.Truck, false);
        }
    }
}
