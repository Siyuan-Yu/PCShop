using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Inventory;
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
        /// <summary>店里货架上卖的杂货（礼品 id）。</summary>
        Goods,
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

        /// <summary>送到哪家店（铺面 id）；null = 主店。</summary>
        public string ShopId;
        public long DueMinute;
        public readonly List<CourierItem> Items = new List<CourierItem>();

        public double LoadKg => Items.Sum(GameSession.CourierItemKg);
        public double FreeKg => Math.Max(0, GameSession.CourierCapacityKg - LoadKg);
    }

    /// <summary>
    /// 买东西的两种拿法：装进背包自己带（算负重），或者叫车送到家里 / 任意一家自己的店。
    /// 送到家：吃的进厨房，礼品、配件进纸箱；送到店：配件上货架，吃的、礼品进店里储物柜。家具都进家具背包。
    /// 叫的是面包车，每辆最多装 <see cref="CourierCapacityKg"/> 公斤，运费按路程算（同打车计价再加跑腿费）。
    /// 在同一个地方接着装的东西先往当前这辆里装，装不下就再叫一辆（再付一次运费）；离开这里后再装就是新车。
    /// </summary>
    public sealed partial class GameSession
    {
        public static readonly Money CourierHandling = Money.FromYuan(20);
        public const int CourierBaseMinutes = 20;
        public const double CourierMetersPerMinute = 400;
        public const double CourierCapacityKg = 200;

        /// <summary><see cref="CourierDest"/> 的两个特殊值：送家里、送当前这家店。其余值是店的铺面 id。</summary>
        public const string CourierHome = "home", CourierCurrentShop = "shop";

        public static double CourierItemKg(CourierItem it)
        {
            switch (it.Kind)
            {
                case CourierItemKind.Rice: return it.Jin * Backpack.RiceKgPerJin;
                case CourierItemKind.Ingredient: return it.Count * Backpack.IngredientKg(it.Id);
                case CourierItemKind.Gift:
                case CourierItemKind.Goods: return it.Count * (Gifts.Get(it.Id)?.WeightKg ?? 0);
                case CourierItemKind.Furniture: return it.Count * (FurnitureCatalog.Get(it.Id)?.WeightKg ?? 0);
                default:
                    PartDef p = PartCatalog.Exists(it.Id) ? PartCatalog.Get(it.Id) : null;
                    return p == null ? 0 : PCShopSell.Core.Inventory.Stock.SpaceOf(p, it.Boxed, it.Count) * Backpack.KgPerSpace;
            }
        }

        private readonly List<CourierRun> _couriers = new List<CourierRun>();

        /// <summary>这次离开这个地方前，正在装货的车（按目的地分：家里是 <see cref="CourierHome"/>，店是铺面 id）。</summary>
        private readonly Dictionary<string, CourierRun> _openRuns = new Dictionary<string, CourierRun>();

        /// <summary>结账时已经一起付过运费、还没用上的车（买东西时先用这些，不再单独收运费）。</summary>
        private int _prepaidCars;

        /// <summary>买东西时叫车送回去（而不是装进背包）。</summary>
        public bool SendByCourier { get; set; }

        /// <summary>叫车送到哪：<see cref="CourierHome"/>、<see cref="CourierCurrentShop"/> 或某家店的铺面 id；null = 按这里的默认（档口送店里，其他送家里）。每进一个地方重置。</summary>
        public string CourierDest { get; set; }

        /// <summary>叫车实际送到哪家店；null = 送家里（defaultToShop 是这里的默认）。还没租铺子时只能送家里。</summary>
        public OwnedShop CourierShop(bool defaultToShop)
        {
            if (!HasShop) return null;
            string d = CourierDest ?? (defaultToShop ? CourierCurrentShop : CourierHome);
            if (d == CourierHome) return null;
            if (d == CourierCurrentShop) return _focus;
            return ShopAt(d) ?? (CourierDest == null || defaultToShop ? _focus : null);
        }

        public bool CourierToShop(bool defaultToShop) => CourierShop(defaultToShop) != null;

        /// <summary>选“送到哪”：null = 家里。</summary>
        public void SetCourierDest(OwnedShop dest) => CourierDest = dest == null ? CourierHome : dest.Place.Id;

        /// <summary>“送到”里的名字：家里 / 我的店里（铺子名）；开了几家店时带上是哪家。</summary>
        public string CourierDestName(OwnedShop dest) =>
            dest == null ? "家里" : MyShopCount > 1 ? $"{ShopTitle(dest)}（{dest.Place.Name}）" : $"我的店里（{dest.Place.Name}）";

        /// <summary>这辆车送去哪（给界面列在路上的车用）。</summary>
        public string CourierRunDestName(CourierRun r) => r.ToShop ? CourierDestName(DeliveryShop(r.ShopId)) : "家里";

        /// <summary>这辆车是不是送进当前这家店（算在途库存、仓位用）。</summary>
        private bool HeadsToThisShop(CourierRun r) => r.ToShop && DeliveryShop(r.ShopId) == _focus;

        private static string RunKey(OwnedShop dest) => dest == null ? CourierHome : dest.Place.Id;

        private OwnedShop AsDest(bool toShop) => toShop && HasShop ? _focus : null;

        public IReadOnlyList<CourierRun> Couriers => _couriers;

        private double CourierDistance(OwnedShop dest)
        {
            string from = CurrentPlace?.NodeId ?? PlayerNodeId;
            string to = dest != null ? dest.Place.NodeId : Home.NodeId;
            PathResult path = Pathfinder.FindPath(City.Map, from, to);
            return path.Found ? path.Distance : 0;
        }

        /// <summary>从这里叫一车送到某家店（null = 家里）：每辆运费和大概多久到。</summary>
        public (Money fee, int minutes) CourierQuote(OwnedShop dest)
        {
            double d = CourierDistance(dest);
            return (Vehicles.TaxiFare(d) + CourierHandling, CourierBaseMinutes + (int)Math.Ceiling(d / CourierMetersPerMinute));
        }

        public (Money fee, int minutes) CourierQuote(bool toShop) => CourierQuote(AsDest(toShop));

        /// <summary>这一车已经叫了（再装的东西先往里装，装得下就不用再付运费）。</summary>
        public bool CourierRunOpen(OwnedShop dest) => _openRuns.ContainsKey(RunKey(dest));

        public bool CourierRunOpen(bool toShop) => CourierRunOpen(AsDest(toShop));

        /// <summary>在这里叫的、还在装货的那辆车；没叫则为 null。</summary>
        public CourierRun OpenCourierRun(OwnedShop dest) => _openRuns.TryGetValue(RunKey(dest), out CourierRun r) ? r : null;

        public CourierRun OpenCourierRun(bool toShop) => OpenCourierRun(AsDest(toShop));

        /// <summary>在这里一共叫了几辆车（到这个目的地的）。</summary>
        public int CourierRunsHere(OwnedShop dest) =>
            _couriers.Count(r => (dest == null ? !r.ToShop : r.ToShop && DeliveryShop(r.ShopId) == dest)
                && r.FromId == CurrentPlace?.Id && r.DueMinute > Clock.Now.TotalMinutes);

        public int CourierRunsHere(bool toShop) => CourierRunsHere(AsDest(toShop));

        private static CourierItem Portion(CourierItem it, double amount) => new CourierItem
        {
            Kind = it.Kind,
            Id = it.Id,
            Count = it.Kind == CourierItemKind.Rice ? 0 : (int)Math.Round(amount),
            Jin = it.Kind == CourierItemKind.Rice ? amount : 0,
            Boxed = it.Boxed,
            ExpireDay = it.ExpireDay,
        };

        /// <summary>一辆车剩 freeKg 还能装多少（米按半斤，其余按件，不拆单件）。</summary>
        private static double FitAmount(bool rice, double unitKg, double freeKg, double left)
        {
            if (unitKg <= 0) return left;
            double n = freeKg / unitKg;
            n = rice ? Math.Floor(n * 2 + 1e-9) / 2 : Math.Floor(n + 1e-9);
            return Math.Min(left, n);
        }

        /// <summary>
        /// 把这批东西装车：先装当前这辆，装不下的按每辆 <see cref="CourierCapacityKg"/> 公斤再叫车（单件不拆）。
        /// 返回每辆车装多少（null 表示是新车），单件就超载时返回 null。
        /// </summary>
        private List<(CourierRun run, double amount)> PlanLoad(CourierItem item, OwnedShop dest)
        {
            bool rice = item.Kind == CourierItemKind.Rice;
            double total = rice ? item.Jin : item.Count;
            double unitKg = total <= 0 ? 0 : CourierItemKg(item) / total;
            if (!rice && unitKg > CourierCapacityKg + 1e-9) return null;
            var plan = new List<(CourierRun, double)>();
            double left = total;
            CourierRun open = OpenCourierRun(dest);
            if (open != null)
            {
                double n = FitAmount(rice, unitKg, open.FreeKg, left);
                if (n > 0) { plan.Add((open, n)); left -= n; }
            }
            while (left > 1e-9)
            {
                double n = FitAmount(rice, unitKg, CourierCapacityKg, left);
                if (n <= 0) return null;
                plan.Add((null, n));
                left -= n;
            }
            return plan;
        }

        /// <summary>这一批（按顺序装）一共要再叫几辆新车；有单件超过一辆车的返回 -1。</summary>
        private int CarsFor(IEnumerable<(bool rice, double unitKg, double amount)> lines, OwnedShop dest)
        {
            double free = OpenCourierRun(dest)?.FreeKg ?? 0;
            int cars = 0;
            foreach (var (rice, unitKg, amount) in lines)
            {
                if (!rice && unitKg > CourierCapacityKg + 1e-9) return -1;
                double left = amount;
                double n = FitAmount(rice, unitKg, free, left);
                left -= n;
                free -= n * unitKg;
                while (left > 1e-9)
                {
                    n = FitAmount(rice, unitKg, CourierCapacityKg, left);
                    if (n <= 0) return -1;
                    cars++;
                    left -= n;
                    free = CourierCapacityKg - n * unitKg;
                }
            }
            return cars;
        }

        /// <summary>这批东西叫车送要再叫几辆新车（0 = 当前这辆装得下）。</summary>
        public int NewCarsNeeded(CourierItem item, OwnedShop dest) => PlanLoad(item, dest)?.Count(p => p.run == null) ?? -1;

        public int NewCarsNeeded(CourierItem item, bool toShop) => NewCarsNeeded(item, AsDest(toShop));

        /// <summary>付货款和新车的运费，把东西装上车。</summary>
        private bool PayAndSend(string category, Money goods, string what, bool defaultToShop, CourierItem item) =>
            Ship(CourierShop(defaultToShop), category, goods, what, item, "买了");

        /// <summary>
        /// 把一批东西装上去 dest（null = 家里）的车：付货款（可以是 0）和新车的运费；结账时已经付过的车先用掉。
        /// </summary>
        private bool Ship(OwnedShop dest, string category, Money goods, string what, CourierItem item, string verb)
        {
            var plan = PlanLoad(item, dest);
            if (plan == null) return Fail($"单件就超过一辆面包车能装的 {CourierCapacityKg:0} 公斤。");
            int newCars = plan.Count(p => p.run == null);
            int prepaid = Math.Min(newCars, _prepaidCars);
            (Money fee, int minutes) = CourierQuote(dest);
            Money freight = fee * (newCars - prepaid);
            Money due = goods + freight;
            if (Ledger.Balance < due) return Fail(newCars > prepaid ? $"要 {goods} + 运费 {freight}（{newCars - prepaid} 辆车），钱不够。" : $"要 {goods}，钱不够。");
            _prepaidCars -= prepaid;
            if (goods.IsPositive) Ledger.TryDebit(Clock.Today, category, goods, what);
            string destName = CourierDestName(dest);
            if (freight.IsPositive)
                Ledger.TryDebit(Clock.Today, "运费", freight, $"叫 {newCars - prepaid} 辆车从{CurrentPlace?.Name ?? "街上"}送到{destName}");
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
                        ToShop = dest != null,
                        ShopId = dest?.Place.Id,
                        DueMinute = Clock.Now.TotalMinutes + minutes,
                    };
                    _couriers.Add(last);
                }
                last.Items.Add(Portion(item, amount));
            }
            _openRuns[RunKey(dest)] = last;
            if (newCars == 0)
                Say($"{verb}{what}，装上刚叫的车（{last.LoadKg:0.#}/{CourierCapacityKg:0} 公斤），一起送到{destName}。");
            else if (plan[0].run != null || newCars > 1)
                Say($"{verb}{what}，车装不下了，又叫了 {newCars} 辆（运费 {fee * newCars}），大约 {minutes} 分钟后送到{destName}。现在这辆装了 {last.LoadKg:0.#}/{CourierCapacityKg:0} 公斤。");
            else
                Say($"{verb}{what}，叫了一辆面包车（运费 {fee}，最多装 {CourierCapacityKg:0} 公斤），大约 {minutes} 分钟后送到{destName}。在这里接着装的先往这辆里装。");
            return true;
        }

        /// <summary>离开这里：这里的车装好出发了，之后再装要重新叫车。</summary>
        private void CloseCourierRuns() => _openRuns.Clear();

        private void DeliverCouriers(GameTimestamp now)
        {
            for (int i = 0; i < _couriers.Count; i++)
            {
                CourierRun run = _couriers[i];
                if (run.DueMinute > now.TotalMinutes) continue;
                foreach (var kv in _openRuns.Where(kv => kv.Value == run).ToList()) _openRuns.Remove(kv.Key);
                _couriers.RemoveAt(i--);
                var what = new List<string>();
                OwnedShop dest = run.ToShop ? DeliveryShop(run.ShopId) : null;
                bool toShop = dest != null && dest.Rented;
                WithShop(dest, () => { foreach (CourierItem it in run.Items) what.Add(Unpack(it, toShop)); });
                string where = !toShop ? "家里" : MyShopCount > 1 ? ShopTitle(dest) : "店里";
                Show("货送到了", $"从{run.FromName}叫的车把东西送到{where}了：\n" + string.Join("\n", what) + "。");
            }
        }

        /// <summary>
        /// 送到的一样东西放好：送家里，米菜进厨房，礼品、配件进纸箱；送店里，配件上货架，米菜礼品进店里纸箱。
        /// 纸箱放不下的进临时存放（下次打开纸箱要清空）。家具都进“我的”待摆放。
        /// </summary>
        private string Unpack(CourierItem it, bool toShop)
        {
            Backpack box = toShop ? ShopLocker : HomeBox;
            Backpack temp = toShop ? ShopTemp : HomeTemp;
            string boxPlace = toShop ? "放进店里纸箱" : "放进纸箱";
            string Where(int overflow, int total) =>
                overflow <= 0 ? boxPlace : overflow >= total ? "纸箱满了，放在临时存放" : $"纸箱放不下的 {overflow} 件放在临时存放";
            switch (it.Kind)
            {
                case CourierItemKind.Rice:
                    if (!toShop)
                    {
                        Pantry.RiceJin += it.Jin;
                        return $"大米 {it.Jin:0.#} 斤（进了厨房）";
                    }
                    double riceOver = StoreRiceOrTemp(true, it.Jin);
                    return $"大米 {it.Jin:0.#} 斤（{(riceOver <= 1e-9 ? boxPlace : $"纸箱放不下的 {riceOver:0.#} 斤放在临时存放")}）";
                case CourierItemKind.Ingredient:
                    IngredientDef d = Ingredients.Get(it.Id);
                    FoodBatch Batch(int n) => new FoodBatch { Id = it.Id, Name = d?.Name ?? it.Id, Count = n, ExpireDay = it.ExpireDay };
                    if (!toShop)
                    {
                        Pantry.Ingredient.Add(Batch(it.Count));
                        return $"{d?.Name ?? it.Id}×{it.Count}（进了厨房）";
                    }
                    int ingOver = StoreOrTemp(true, Backpack.IngredientKg(it.Id), it.Count, n => box.Food.Ingredient.Add(Batch(n)), n => temp.Food.Ingredient.Add(Batch(n)));
                    return $"{d?.Name ?? it.Id}×{it.Count}（{Where(ingOver, it.Count)}）";
                case CourierItemKind.Gift:
                    int giftOver = StoreOrTemp(toShop, Gifts.Get(it.Id)?.WeightKg ?? 0, it.Count, n => box.AddGift(it.Id, n), n => temp.AddGift(it.Id, n));
                    return $"{Gifts.Get(it.Id)?.Name ?? it.Id}×{it.Count}（{Where(giftOver, it.Count)}）";
                case CourierItemKind.Goods:
                    if (toShop)
                    {
                        PlayerShop.Stock.ForceAddGoods(it.Id, it.Count);
                        return $"{Gifts.Get(it.Id)?.Name ?? it.Id}×{it.Count}（进了店里后仓）";
                    }
                    int goodsOver = StoreOrTemp(false, Gifts.Get(it.Id)?.WeightKg ?? 0, it.Count, n => HomeBox.AddGift(it.Id, n), n => HomeTemp.AddGift(it.Id, n));
                    return $"{Gifts.Get(it.Id)?.Name ?? it.Id}×{it.Count}（{Where(goodsOver, it.Count)}）";
                case CourierItemKind.Furniture:
                    AddFurniture(it.Id, it.Count);
                    return $"{FurnitureCatalog.Get(it.Id)?.Name ?? it.Id}×{it.Count}（进了“我的”家具库，在家里能查看，摆要去用得上的地方）";
                default:
                    PartDef p = PartCatalog.Get(it.Id);
                    if (!toShop)
                    {
                        int partOver = StorePartAtHome(p, it.Boxed, it.Count);
                        return $"{p.Name}×{it.Count}（{Where(partOver, it.Count)}）";
                    }
                    PlayerShop.Stock.ForceAdd(p, it.Boxed, it.Count);
                    return $"{p.Name}×{it.Count}（上了货架）";
            }
        }

        /// <summary>配件送到家：能放进纸箱的放纸箱，剩下的进临时存放。返回进临时存放的件数。</summary>
        private int StorePartAtHome(PartDef p, bool boxed, int count) =>
            StoreOrTemp(false, Stock.SpaceOf(p, boxed, 1) * Backpack.KgPerSpace, count, n => HomeBoxParts.ForceAdd(p, boxed, n), n => HomeTempParts.ForceAdd(p, boxed, n));

        private SaveCourier[] SaveCouriers() => _couriers.Select(r => new SaveCourier
        {
            fromId = r.FromId,
            fromName = r.FromName,
            toShop = r.ToShop,
            shopId = r.ShopId,
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
                var run = new CourierRun { FromId = s.fromId, FromName = s.fromName, ToShop = s.toShop, ShopId = string.IsNullOrEmpty(s.shopId) ? null : s.shopId, DueMinute = s.dueMinute };
                int n = s.kinds?.Length ?? 0;
                for (int i = 0; i < n; i++)
                {
                    var kind = (CourierItemKind)s.kinds[i];
                    string id = s.ids != null && i < s.ids.Length ? s.ids[i] : null;
                    if (!Enum.IsDefined(typeof(CourierItemKind), kind)) continue;
                    if (kind == CourierItemKind.Part && (id == null || !PartCatalog.Exists(id))) continue;
                    if ((kind == CourierItemKind.Gift || kind == CourierItemKind.Goods) && Gifts.Get(id) == null) continue;
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
