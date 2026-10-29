using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    public enum CourierItemKind
    {
        Rice,
        Ingredient,
        Gift,
        Part,
        Furniture,
    }

    public sealed class CourierItem
    {
        public CourierItemKind Kind;
        public string Id;
        public int Count;
        public double Jin;
        public bool Boxed;
        public int ExpireDay;
    }

    /// <summary>叫来的一辆面包车：在同一家店里接着买的东西往里装，装满了要再叫一辆。</summary>
    public sealed class CourierRun
    {
        public string FromId;
        public string FromName;
        public bool ToShop;
        public long DueMinute;
        public readonly List<CourierItem> Items = new List<CourierItem>();

        public double LoadKg => Items.Sum(GameSession.CourierItemKg);
        public double FreeKg => Math.Max(0, GameSession.CourierCapacityKg - LoadKg);
    }

    /// <summary>
    /// 买东西的两种拿法：装进背包自己带（算负重），或者叫车送到家里 / 店里（每家店都能选）。
    /// 送到家：吃的进厨房，礼品、配件进纸箱；送到店：配件上货架，吃的、礼品进店里储物柜。家具都进家具背包。
    /// 叫的是面包车，每辆最多装 <see cref="CourierCapacityKg"/> 公斤，运费按路程算（同打车计价再加跑腿费）。
    /// 在同一家店里接着买的东西先往当前这辆里装，装不下就再叫一辆（再付一次运费）；离开这家店后再买就是新车。
    /// </summary>
    public sealed partial class GameSession
    {
        public static readonly Money CourierHandling = Money.FromYuan(20);
        public const int CourierBaseMinutes = 20;
        public const double CourierMetersPerMinute = 400;
        public const double CourierCapacityKg = 200;

        public static double CourierItemKg(CourierItem it)
        {
            switch (it.Kind)
            {
                case CourierItemKind.Rice: return it.Jin * Backpack.RiceKgPerJin;
                case CourierItemKind.Ingredient: return it.Count * Backpack.IngredientKg(it.Id);
                case CourierItemKind.Gift: return it.Count * (Gifts.Get(it.Id)?.WeightKg ?? 0);
                case CourierItemKind.Furniture: return it.Count * (FurnitureCatalog.Get(it.Id)?.WeightKg ?? 0);
                default:
                    PartDef p = PartCatalog.Exists(it.Id) ? PartCatalog.Get(it.Id) : null;
                    return p == null ? 0 : PCShopSell.Core.Inventory.Stock.SpaceOf(p, it.Boxed, it.Count) * Backpack.KgPerSpace;
            }
        }

        private readonly List<CourierRun> _couriers = new List<CourierRun>();

        /// <summary>这次离开店铺前，正在拼单的车（按目的地分）。</summary>
        private readonly Dictionary<bool, CourierRun> _openRuns = new Dictionary<bool, CourierRun>();

        /// <summary>买东西时叫车送回去（而不是装进背包）。</summary>
        public bool SendByCourier { get; set; }

        /// <summary>叫车送到哪：true = 店里，false = 家里，null = 按这家店的默认（档口送店里，其他送家里）。每进一家店重置。</summary>
        public bool? CourierDest { get; set; }

        /// <summary>这家店叫车实际送到哪（defaultToShop 是这家店的默认）。</summary>
        public bool CourierToShop(bool defaultToShop) => CourierDest ?? defaultToShop;

        public IReadOnlyList<CourierRun> Couriers => _couriers;

        private double CourierDistance(bool toShop)
        {
            string from = CurrentPlace?.NodeId ?? PlayerNodeId;
            string to = toShop ? PlayerShopPlace.NodeId : Home.NodeId;
            PathResult path = Pathfinder.FindPath(City.Map, from, to);
            return path.Found ? path.Distance : 0;
        }

        /// <summary>从这里叫一车送到店里 / 家里：运费和大概多久到。</summary>
        public (Money fee, int minutes) CourierQuote(bool toShop)
        {
            double d = CourierDistance(toShop);
            return (Vehicles.TaxiFare(d) + CourierHandling, CourierBaseMinutes + (int)Math.Ceiling(d / CourierMetersPerMinute));
        }

        /// <summary>这一车已经叫了（再买的东西先往里装，装得下就不用再付运费）。</summary>
        public bool CourierRunOpen(bool toShop) => _openRuns.ContainsKey(toShop);

        /// <summary>在这家店里叫的、还在装货的那辆车；没叫则为 null。</summary>
        public CourierRun OpenCourierRun(bool toShop) => _openRuns.TryGetValue(toShop, out CourierRun r) ? r : null;

        /// <summary>在这家店里一共叫了几辆车（到目的地的）。</summary>
        public int CourierRunsHere(bool toShop) =>
            _couriers.Count(r => r.ToShop == toShop && r.FromId == CurrentPlace?.Id && r.DueMinute > Clock.Now.TotalMinutes);

        private static CourierItem Portion(CourierItem it, double amount) => new CourierItem
        {
            Kind = it.Kind,
            Id = it.Id,
            Count = it.Kind == CourierItemKind.Rice ? 0 : (int)Math.Round(amount),
            Jin = it.Kind == CourierItemKind.Rice ? amount : 0,
            Boxed = it.Boxed,
            ExpireDay = it.ExpireDay,
        };

        /// <summary>
        /// 把这批东西装车：先装当前这辆，装不下的按每辆 <see cref="CourierCapacityKg"/> 公斤再叫车（单件不拆）。
        /// 返回每辆车装多少（null 表示是新车），单件就超载时返回 null。
        /// </summary>
        private List<(CourierRun run, double amount)> PlanLoad(CourierItem item, bool toShop)
        {
            bool rice = item.Kind == CourierItemKind.Rice;
            double total = rice ? item.Jin : item.Count;
            double unitKg = total <= 0 ? 0 : CourierItemKg(item) / total;
            if (!rice && unitKg > CourierCapacityKg + 1e-9) return null;
            var plan = new List<(CourierRun, double)>();
            double left = total;
            double Fit(double freeKg)
            {
                if (unitKg <= 0) return left;
                double n = freeKg / unitKg;
                n = rice ? Math.Floor(n * 2 + 1e-9) / 2 : Math.Floor(n + 1e-9);
                return Math.Min(left, n);
            }
            CourierRun open = OpenCourierRun(toShop);
            if (open != null)
            {
                double n = Fit(open.FreeKg);
                if (n > 0) { plan.Add((open, n)); left -= n; }
            }
            while (left > 1e-9)
            {
                double n = Fit(CourierCapacityKg);
                if (n <= 0) return null;
                plan.Add((null, n));
                left -= n;
            }
            return plan;
        }

        /// <summary>这批东西叫车送要再叫几辆新车（0 = 当前这辆装得下）。</summary>
        public int NewCarsNeeded(CourierItem item, bool toShop) => PlanLoad(item, toShop)?.Count(p => p.run == null) ?? -1;

        /// <summary>付货款和新车的运费，把东西装上车。</summary>
        private bool PayAndSend(string category, Money goods, string what, bool defaultToShop, CourierItem item)
        {
            bool toShop = CourierToShop(defaultToShop);
            var plan = PlanLoad(item, toShop);
            if (plan == null) return Fail($"单件就超过一辆面包车能装的 {CourierCapacityKg:0} 公斤。");
            int newCars = plan.Count(p => p.run == null);
            (Money fee, int minutes) = CourierQuote(toShop);
            Money freight = fee * newCars;
            Money due = goods + freight;
            if (Ledger.Balance < due) return Fail(newCars > 0 ? $"要 {goods} + 运费 {freight}（{newCars} 辆车），钱不够。" : $"要 {goods}，钱不够。");
            Ledger.TryDebit(Clock.Today, category, goods, what);
            string dest = toShop ? "店里" : "家里";
            if (newCars > 0)
                Ledger.TryDebit(Clock.Today, "运费", freight, $"叫 {newCars} 辆车从{CurrentPlace?.Name}送到{dest}");
            CourierRun last = null;
            foreach (var (run, amount) in plan)
            {
                last = run;
                if (last == null)
                {
                    last = new CourierRun
                    {
                        FromId = CurrentPlace?.Id,
                        FromName = CurrentPlace?.Name ?? "街上",
                        ToShop = toShop,
                        DueMinute = Clock.Now.TotalMinutes + minutes,
                    };
                    _couriers.Add(last);
                }
                last.Items.Add(Portion(item, amount));
            }
            _openRuns[toShop] = last;
            if (newCars == 0)
                Say($"买了{what}，装上刚叫的车（{last.LoadKg:0.#}/{CourierCapacityKg:0} 公斤），一起送到{dest}。");
            else if (plan[0].run != null || newCars > 1)
                Say($"买了{what}，车装不下了，又叫了 {newCars} 辆（运费 {freight}），大约 {minutes} 分钟后送到{dest}。现在这辆装了 {last.LoadKg:0.#}/{CourierCapacityKg:0} 公斤。");
            else
                Say($"买了{what}，叫了一辆面包车（运费 {fee}，最多装 {CourierCapacityKg:0} 公斤），大约 {minutes} 分钟后送到{dest}。在这家接着买的先往这辆里装。");
            return true;
        }

        /// <summary>离开店铺：这家的车装好出发了，之后再买要重新叫车。</summary>
        private void CloseCourierRuns() => _openRuns.Clear();

        private void DeliverCouriers(GameTimestamp now)
        {
            for (int i = 0; i < _couriers.Count; i++)
            {
                CourierRun run = _couriers[i];
                if (run.DueMinute > now.TotalMinutes) continue;
                if (_openRuns.TryGetValue(run.ToShop, out CourierRun open) && open == run) _openRuns.Remove(run.ToShop);
                _couriers.RemoveAt(i--);
                var what = new List<string>();
                foreach (CourierItem it in run.Items) what.Add(Unpack(it, run.ToShop));
                Say($"车把{run.FromName}买的东西送到{(run.ToShop ? "店里" : "家里")}了：" + string.Join("、", what) + "。");
            }
        }

        private string Unpack(CourierItem it, bool toShop)
        {
            Pantry food = toShop ? ShopLocker.Food : Pantry;
            string foodPlace = toShop ? "放进店里储物柜" : "进了厨房";
            switch (it.Kind)
            {
                case CourierItemKind.Rice:
                    food.RiceJin += it.Jin;
                    return $"大米 {it.Jin:0.#} 斤（{foodPlace}）";
                case CourierItemKind.Ingredient:
                    IngredientDef d = Ingredients.Get(it.Id);
                    food.Ingredient.Add(new FoodBatch { Id = it.Id, Name = d?.Name ?? it.Id, Count = it.Count, ExpireDay = it.ExpireDay });
                    return $"{d?.Name ?? it.Id}×{it.Count}（{foodPlace}）";
                case CourierItemKind.Gift:
                    (toShop ? ShopLocker : HomeBox).AddGift(it.Id, it.Count);
                    return $"{Gifts.Get(it.Id)?.Name ?? it.Id}×{it.Count}（{(toShop ? "放进店里储物柜" : "放进纸箱")}）";
                case CourierItemKind.Furniture:
                    AddFurniture(it.Id, it.Count);
                    return $"{FurnitureCatalog.Get(it.Id)?.Name ?? it.Id}×{it.Count}（在“我的”里摆放）";
                default:
                    PartDef p = PartCatalog.Get(it.Id);
                    if (!toShop)
                    {
                        HomeBoxParts.ForceAdd(p, it.Boxed, it.Count);
                        return $"{p.Name}×{it.Count}（放在家里纸箱）";
                    }
                    PlayerShop.Stock.ForceAdd(p, it.Boxed, it.Count);
                    return $"{p.Name}×{it.Count}（上了货架）";
            }
        }

        private SaveCourier[] SaveCouriers() => _couriers.Select(r => new SaveCourier
        {
            fromId = r.FromId,
            fromName = r.FromName,
            toShop = r.ToShop,
            dueMinute = r.DueMinute,
            kinds = r.Items.Select(i => (int)i.Kind).ToArray(),
            ids = r.Items.Select(i => i.Id).ToArray(),
            counts = r.Items.Select(i => i.Count).ToArray(),
            jins = r.Items.Select(i => i.Jin).ToArray(),
            boxed = r.Items.Select(i => i.Boxed).ToArray(),
            expireDays = r.Items.Select(i => i.ExpireDay).ToArray(),
        }).ToArray();

        private void RestoreCouriers(SaveCourier[] saved)
        {
            _couriers.Clear();
            _openRuns.Clear();
            foreach (SaveCourier s in saved ?? Array.Empty<SaveCourier>())
            {
                var run = new CourierRun { FromId = s.fromId, FromName = s.fromName, ToShop = s.toShop, DueMinute = s.dueMinute };
                int n = s.kinds?.Length ?? 0;
                for (int i = 0; i < n; i++)
                {
                    var kind = (CourierItemKind)s.kinds[i];
                    string id = s.ids != null && i < s.ids.Length ? s.ids[i] : null;
                    if (kind == CourierItemKind.Part && (id == null || !PartCatalog.Exists(id))) continue;
                    if (kind == CourierItemKind.Gift && Gifts.Get(id) == null) continue;
                    if (kind == CourierItemKind.Furniture && FurnitureCatalog.Get(id) == null) continue;
                    run.Items.Add(new CourierItem
                    {
                        Kind = kind,
                        Id = id,
                        Count = s.counts != null && i < s.counts.Length ? s.counts[i] : 0,
                        Jin = s.jins != null && i < s.jins.Length ? s.jins[i] : 0,
                        Boxed = s.boxed != null && i < s.boxed.Length && s.boxed[i],
                        ExpireDay = s.expireDays != null && i < s.expireDays.Length ? s.expireDays[i] : 0,
                    });
                }
                if (run.Items.Count > 0) _couriers.Add(run);
            }
        }
    }
}
