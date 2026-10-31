using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    /// <summary>购物车 / 货车装货单里的一行。</summary>
    public sealed class CartLine
    {
        public string Key;
        public string Name;

        /// <summary>件数；按斤的（大米）是斤数。</summary>
        public double Amount;

        /// <summary>其中自己装进背包的部分；剩下的叫车送。</summary>
        public double InBag;
        public bool ByJin;
        public double UnitKg;

        public double ByTruck => Math.Max(0, Amount - InBag);
        public double BagKg => UnitKg * InBag;
        public double TruckKg => UnitKg * ByTruck;

        /// <summary>单价（随时按当前价算）；搬东西是 null。</summary>
        public Func<Money> UnitPrice;

        /// <summary>结账时真正去做：买下 / 从原处拿出来装车。</summary>
        internal Func<double, bool> Run;

        public double Kg => UnitKg * Amount;
        public Money Price => UnitPrice == null ? Money.Zero : UnitPrice().Scale(Amount);
        public string AmountText => ByJin ? $"{Amount:0.#} 斤" : $"×{Amount:0}";
    }

    /// <summary>这里能交给货车司机搬走的一样东西。</summary>
    public sealed class MovableItem
    {
        public string Key;
        public string Name;
        public string Where;
        public double Have;
        public bool ByJin;
        public double UnitKg;
    }

    /// <summary>
    /// 店里买东西先放购物车，点“结账”一起付：货款 + 按总重算的车数 × 每辆运费（叫车时），或者一起装进背包。
    /// 打电话叫货车搬东西用同一套：先把要搬的装进装货单，选好送到哪，点“出发”一起付运费。
    /// </summary>
    public sealed partial class GameSession
    {
        /// <summary>通讯录里随时能叫的货车司机。</summary>
        public const string TruckDriverName = "货车司机老蔡";

        private readonly List<CartLine> _cart = new List<CartLine>();
        private readonly List<CartLine> _truck = new List<CartLine>();

        public IReadOnlyList<CartLine> Cart => _cart;
        public IReadOnlyList<CartLine> TruckLoad => _truck;

        private void ClearCarts()
        {
            _cart.Clear();
            _truck.Clear();
        }

        public void ClearCart() => _cart.Clear();
        public void ClearTruckLoad() => _truck.Clear();

        public double CartAmount(string key) => _cart.Find(l => l.Key == key)?.Amount ?? 0;
        public double TruckAmount(string key) => _truck.Find(l => l.Key == key)?.Amount ?? 0;

        public bool RemoveFromCart(string key, double amount) => TakeLine(_cart, key, amount);
        public bool RemoveFromTruck(string key, double amount) => TakeLine(_truck, key, amount);

        public double CartKg => _cart.Sum(l => l.Kg);
        public double CartBagKg => _cart.Sum(l => l.BagKg);
        public double CartTruckKg => _cart.Sum(l => l.TruckKg);
        public double TruckKg => _truck.Sum(l => l.Kg);
        public Money CartGoods => _cart.Aggregate(Money.Zero, (sum, l) => sum + l.Price);

        /// <summary>背包还能再放多少公斤（已经拖进背包栏的算上）。</summary>
        public double CartBagFreeKg => Math.Max(0, FreeKg - CartBagKg);

        /// <summary>把购物车里这一样拿 amount 放进背包栏（其余仍叫车）。</summary>
        public bool CartToBag(string key, double amount)
        {
            CartLine l = _cart.Find(x => x.Key == key);
            if (l == null || amount <= 0) return false;
            l.InBag = Math.Min(l.Amount, l.InBag + amount);
            return true;
        }

        /// <summary>从背包栏拿回 amount 改成叫车。</summary>
        public bool CartToTruck(string key, double amount)
        {
            CartLine l = _cart.Find(x => x.Key == key);
            if (l == null || amount <= 0) return false;
            l.InBag = Math.Max(0, l.InBag - amount);
            return true;
        }

        /// <summary>背得动的都放进背包栏（按购物车顺序，单件不拆），剩下的叫车。</summary>
        public void CartFillBag()
        {
            foreach (CartLine l in _cart)
            {
                double n = FitAmount(l.ByJin, l.UnitKg, CartBagFreeKg, l.ByTruck);
                if (n > 0) l.InBag += n;
            }
        }

        public void CartAllToTruck()
        {
            foreach (CartLine l in _cart) l.InBag = 0;
        }

        private static bool TakeLine(List<CartLine> list, string key, double amount)
        {
            CartLine l = list.Find(x => x.Key == key);
            if (l == null || amount <= 0) return false;
            l.Amount -= amount;
            l.InBag = Math.Min(l.InBag, Math.Max(0, l.Amount));
            if (l.Amount <= 1e-9) list.Remove(l);
            return true;
        }

        private static void PutLine(List<CartLine> list, CartLine proto, double amount)
        {
            CartLine l = list.Find(x => x.Key == proto.Key);
            if (l != null)
            {
                l.Amount += amount;
                return;
            }
            proto.Amount = amount;
            list.Add(proto);
        }

        private int CarsFor(List<CartLine> lines, OwnedShop dest) =>
            CarsFor(lines.Where(l => l.ByTruck > 1e-9).Select(l => (l.ByJin, l.UnitKg, l.ByTruck)), dest);

        /// <summary>购物车里叫车的部分要几辆新车（-1 = 有单件超过一辆车）。</summary>
        public int CartCars(bool defaultToShop) => CarsFor(_cart, CourierShop(defaultToShop));

        public Money CartFreight(bool defaultToShop)
        {
            int cars = CartCars(defaultToShop);
            return cars <= 0 ? Money.Zero : CourierQuote(CourierShop(defaultToShop)).fee * cars;
        }

        // ---------- 往购物车里放 ----------

        public bool CartAddStall(Place stall, PartDef part, int count)
        {
            if (stall == null || part == null || count <= 0) return false;
            if (CurrentPlace != stall || !StallSells(stall, part)) return Fail("这家档口不卖这个。");
            string key = "part:" + part.Id;
            int remaining = StallRemaining(stall, part) - (int)Math.Round(CartAmount(key));
            if (count > remaining)
                return Fail(remaining <= 0 ? $"老板说{part.Name}今天不能再多给你了，明天再来。" : $"老板说{part.Name}今天最多再给你 {remaining} 个。");
            PutLine(_cart, new CartLine
            {
                Key = key,
                Name = part.Name,
                UnitKg = Stock.SpaceOf(part, true, 1) * Backpack.KgPerSpace,
                UnitPrice = () => StallPrice(stall, part),
                Run = n => BuyAtStall(stall, part, (int)Math.Round(n)),
            }, count);
            return true;
        }

        public bool CartAddFurniture(string id)
        {
            FurnitureDef d = FurnitureCatalog.Get(id);
            if (d == null || d.Starter) return false;
            if (!AtMall) return Fail("要去家具城才能买。");
            PutLine(_cart, new CartLine
            {
                Key = "furn:" + d.Id,
                Name = d.Name,
                UnitKg = d.WeightKg,
                UnitPrice = () => d.Price,
                Run = n =>
                {
                    bool all = true;
                    for (int i = 0; i < (int)Math.Round(n); i++) all &= BuyFurniture(d.Id);
                    return all;
                },
            }, 1);
            return true;
        }

        public bool CartAddGift(string id, int count = 1)
        {
            GiftDef g = Gifts.Get(id);
            if (g == null || count <= 0) return false;
            if (!AtBusiness(g.SoldAt)) return Fail(g.SoldAt == FarmBusiness ? "水果要去农贸市场买。" : "要去烟酒茶行买。");
            PutLine(_cart, new CartLine
            {
                Key = "gift:" + id,
                Name = g.Name,
                UnitKg = g.WeightKg,
                UnitPrice = () => g.Price,
                Run = n => BuyGift(id, (int)Math.Round(n)),
            }, count);
            return true;
        }

        public bool CartAddRice(double jin)
        {
            if (jin <= 0) return false;
            if (!AtFarmMarket) return Fail("要去农贸市场买。");
            PutLine(_cart, new CartLine
            {
                Key = "rice",
                Name = "大米",
                ByJin = true,
                UnitKg = Backpack.RiceKgPerJin,
                UnitPrice = () => Pantry.RicePricePerJin,
                Run = n => BuyRice(n),
            }, jin);
            return true;
        }

        public bool CartAddIngredient(string id, int count)
        {
            IngredientDef d = Ingredients.Get(id);
            if (d == null || count <= 0) return false;
            if (!AtFarmMarket) return Fail("要去农贸市场买。");
            PutLine(_cart, new CartLine
            {
                Key = "ing:" + id,
                Name = d.Name,
                UnitKg = Backpack.IngredientKg(id),
                UnitPrice = () => d.Price,
                Run = n => BuyIngredient(id, (int)Math.Round(n)),
            }, count);
            return true;
        }

        // ---------- 结账 ----------

        /// <summary>现在能不能结账；能返回 null。</summary>
        public string CheckoutBlocker(bool defaultToShop)
        {
            if (_cart.Count == 0) return "购物车是空的";
            if (CartBagKg > FreeKg + 1e-9)
                return $"背包放不下：拖进背包的有 {CartBagKg:0.#} 公斤，{CurrentVehicle.Name}还能背 {FreeKg:0.#} 公斤。拖一些回去叫车";
            Money due = CartGoods;
            if (CartTruckKg > 1e-9)
            {
                if (CartCars(defaultToShop) < 0) return $"有一件超过一辆面包车能装的 {CourierCapacityKg:0} 公斤";
                due += CartFreight(defaultToShop);
            }
            if (Ledger.Balance < due) return $"一共要 {due}，钱不够";
            return null;
        }

        /// <summary>结账：付货款；背包栏里的装进背包，其余一起叫车（按总重算几辆，运费一次付清），送到选好的地方。</summary>
        public bool Checkout(bool defaultToShop)
        {
            string why = CheckoutBlocker(defaultToShop);
            if (why != null) return Fail(why + "。");
            bool truck = CartTruckKg > 1e-9;
            OwnedShop dest = CourierShop(defaultToShop);
            int cars = truck ? CartCars(defaultToShop) : 0;
            if (truck) SetCourierDest(dest);
            bool keep = SendByCourier;
            try { return RunLines(_cart, dest, cars, "结账"); }
            finally { SendByCourier = keep; }
        }

        /// <summary>一行行去做（背包栏的部分和叫车的部分分开）；先一起付好车钱，没用上的车退钱；最后只报一次总账。</summary>
        private bool RunLines(List<CartLine> lines, OwnedShop dest, int cars, string title)
        {
            (Money fee, int minutes) = CourierQuote(dest);
            string destName = CourierDestName(dest);
            if (cars > 0) Ledger.TryDebit(Clock.Today, "运费", fee * cars, $"叫 {cars} 辆车从{CurrentPlace?.Name ?? "街上"}送到{destName}");
            Money before = Ledger.Balance;
            _prepaidCars = cars;
            var done = new List<string>();
            var failed = new List<string>();
            double bagKg = 0, truckKg = 0;
            string Label(CartLine l, double n) => l.Name + (l.ByJin ? $" {n:0.#} 斤" : n > 1 ? $"×{n:0}" : "");
            _quiet++;
            try
            {
                foreach (CartLine l in lines.ToList())
                {
                    if (l.InBag > 1e-9)
                    {
                        SendByCourier = false;
                        if (l.Run(l.InBag)) { done.Add(Label(l, l.InBag)); bagKg += l.BagKg; }
                        else failed.Add(Label(l, l.InBag));
                    }
                    if (l.ByTruck > 1e-9)
                    {
                        SendByCourier = true;
                        if (l.Run(l.ByTruck)) { done.Add(Label(l, l.ByTruck)); truckKg += l.TruckKg; }
                        else failed.Add(Label(l, l.ByTruck));
                    }
                }
            }
            finally
            {
                _quiet--;
            }
            Money spent = before - Ledger.Balance;
            int unused = _prepaidCars;
            _prepaidCars = 0;
            if (unused > 0)
            {
                Ledger.Credit(Clock.Today, "运费", fee * unused, "没用上的车退运费");
                cars -= unused;
            }
            lines.Clear();
            if (done.Count == 0) return false;
            var tail = new List<string>();
            if (bagKg > 1e-9) tail.Add($"{bagKg:0.#} 公斤装进背包");
            if (truckKg > 1e-9)
                tail.Add(cars > 0 ? $"{truckKg:0.#} 公斤叫了 {cars} 辆面包车（运费 {fee * cars}），大约 {minutes} 分钟送到{destName}"
                    : $"{truckKg:0.#} 公斤装上刚才叫的车，一起送到{destName}");
            Say($"{title}：{string.Join("、", done)}" + (spent.IsPositive ? $"，货款 {spent}" : "") + (tail.Count > 0 ? "；" + string.Join("；", tail) : "") + "。"
                + (failed.Count > 0 ? $"没办成的：{string.Join("、", failed)}。" : ""));
            return true;
        }

        // ---------- 打电话叫货车搬东西 ----------

        /// <summary>货车送到哪：在自己店里默认送家里，别处默认送当前这家店（没店只能送家里）。</summary>
        public OwnedShop TruckDest => CourierShop(!InPlayerShop);

        /// <summary>选的地方就是这里时不能叫。</summary>
        public string TruckDestBlocker =>
            TruckDest == null && AtHome ? "你就在家里，选个别的地方"
            : TruckDest != null && CurrentPlace == TruckDest.Place ? "你就在这家店里，选个别的地方"
            : null;

        public int TruckCars => CarsFor(_truck, TruckDest);

        public Money TruckFreight => TruckCars <= 0 ? Money.Zero : CourierQuote(TruckDest).fee * TruckCars;

        public string TruckBlocker
        {
            get
            {
                if (_truck.Count == 0) return "还没装东西";
                if (TruckDestBlocker != null) return TruckDestBlocker;
                if (TruckCars < 0) return $"有一件超过一辆面包车能装的 {CourierCapacityKg:0} 公斤";
                if (Ledger.Balance < TruckFreight) return $"运费 {TruckFreight}，钱不够";
                return null;
            }
        }

        /// <summary>这里能交给司机的东西：身上的（背包、随身货）随时都行；在家还有厨房和纸箱，在自己店里还有货架后仓和储物柜。</summary>
        public List<MovableItem> MovableHere()
        {
            var keys = new List<string>();
            foreach (string src in new[] { "bag", "temp", "kitchen", "box", "shop", "locker" })
            {
                if (!SourceHere(src)) continue;
                Pantry food = FoodOf(src);
                if (food != null)
                {
                    if (food.RiceJin > 1e-9) keys.Add(src + "|rice");
                    foreach (string id in food.Ingredient.Where(b => b.Count > 0).Select(b => b.Id).Distinct()) keys.Add(src + "|ing:" + id);
                }
                Backpack bag = BagOf(src);
                if (bag != null)
                {
                    foreach (var kv in bag.GiftLines) keys.Add(src + "|gift:" + kv.Key);
                    foreach (var kv in bag.FurnitureLines) keys.Add(src + "|furn:" + kv.Key);
                }
                Stock parts = PartsOf(src);
                if (parts != null)
                {
                    foreach (StockLine l in parts.Lines()) keys.Add($"{src}|part:{l.Part.Id}:{(l.Boxed ? "b" : "l")}");
                    foreach (var kv in parts.GoodsLines()) keys.Add(src + "|goods:" + kv.Key);
                }
            }
            return keys.Select(k => DescribeMove(k, out _)).Where(m => m != null && m.Have > 1e-9).ToList();
        }

        /// <summary>把这里的一样东西装进货车装货单（还没出发，随时可以拿下来）。</summary>
        public bool AddToTruck(string key, double amount)
        {
            MovableItem m = DescribeMove(key, out _);
            if (m == null) return Fail("这里没有这个。");
            if (TruckDestBlocker != null) return Fail(TruckDestBlocker + "。");
            double left = m.Have - TruckAmount(key);
            amount = Math.Min(amount, left);
            if (!m.ByJin) amount = Math.Floor(amount + 1e-9);
            if (amount <= 1e-9) return Fail("已经全装上了。");
            PutLine(_truck, new CartLine
            {
                Key = key,
                Name = $"{m.Name}（{m.Where}）",
                ByJin = m.ByJin,
                UnitKg = m.UnitKg,
                Run = n => ShipFromHere(key, n),
            }, amount);
            return true;
        }

        /// <summary>货车出发：一起付运费，装货单上的东西从原处拿走，送到选好的地方。</summary>
        public bool DispatchTruck()
        {
            string why = TruckBlocker;
            if (why != null) return Fail(why + "。");
            bool keep = SendByCourier;
            try { return RunLines(_truck, TruckDest, TruckCars, $"{TruckDriverName}把东西装上车了"); }
            finally { SendByCourier = keep; }
        }

        private bool SourceHere(string src)
        {
            switch (src)
            {
                case "bag": return true;
                case "temp": return AtHome || InPlayerShop;
                case "kitchen":
                case "box": return AtHome;
                case "shop":
                case "locker": return InPlayerShop;
                default: return false;
            }
        }

        private static string SourceName(string src)
        {
            switch (src)
            {
                case "bag": return "身上";
                case "temp": return "临时存放";
                case "kitchen": return "厨房";
                case "box": return "家里纸箱";
                case "shop": return "店里后仓";
                default: return "店里纸箱";
            }
        }

        private Backpack TempHere => AtHome ? HomeTemp : InPlayerShop ? ShopTemp : null;

        private Pantry FoodOf(string src) =>
            src == "bag" ? Backpack.Food : src == "kitchen" ? Pantry : src == "box" ? HomeBox.Food : src == "locker" ? ShopLocker.Food : src == "temp" ? TempHere?.Food : null;

        private Backpack BagOf(string src) =>
            src == "bag" ? Backpack : src == "box" ? HomeBox : src == "locker" ? ShopLocker : src == "temp" ? TempHere : null;

        private Stock PartsOf(string src) =>
            src == "bag" ? Cargo : src == "box" ? HomeBoxParts : src == "shop" ? PlayerShop.Stock : src == "temp" && AtHome ? HomeTempParts : null;

        private MovableItem DescribeMove(string key, out CourierItem unit)
        {
            unit = null;
            string[] kk = (key ?? "").Split('|');
            if (kk.Length != 2 || !SourceHere(kk[0])) return null;
            string src = kk[0];
            string[] k = kk[1].Split(':');
            string id = k.Length > 1 ? k[1] : null;
            var m = new MovableItem { Key = key, Where = SourceName(src) };
            Pantry food = FoodOf(src);
            Backpack bag = BagOf(src);
            Stock parts = PartsOf(src);
            switch (k[0])
            {
                case "part":
                    if (parts == null || !PartCatalog.Exists(id)) return null;
                    PartDef p = PartCatalog.Get(id);
                    bool boxed = k.Length > 2 && k[2] == "b";
                    m.Have = parts.Count(p, boxed);
                    m.Name = p.Name + (boxed ? "（整盒）" : "（散件）");
                    unit = new CourierItem { Kind = CourierItemKind.Part, Id = id, Boxed = boxed, Count = 1 };
                    break;
                case "goods":
                    if (parts == null || Gifts.Get(id) == null) return null;
                    m.Have = parts.GoodsCount(id);
                    m.Name = Gifts.Get(id).Name;
                    unit = new CourierItem { Kind = CourierItemKind.Goods, Id = id, Count = 1 };
                    break;
                case "rice":
                    if (food == null) return null;
                    m.Have = food.RiceJin;
                    m.Name = "大米";
                    m.ByJin = true;
                    unit = new CourierItem { Kind = CourierItemKind.Rice, Id = "rice", Jin = 1 };
                    break;
                case "ing":
                    if (food == null) return null;
                    m.Have = food.IngredientCount(id);
                    m.Name = Ingredients.Get(id)?.Name ?? id;
                    unit = new CourierItem { Kind = CourierItemKind.Ingredient, Id = id, Count = 1 };
                    break;
                case "gift":
                    if (bag == null || Gifts.Get(id) == null) return null;
                    m.Have = bag.GiftCount(id);
                    m.Name = Gifts.Get(id).Name;
                    unit = new CourierItem { Kind = CourierItemKind.Gift, Id = id, Count = 1 };
                    break;
                case "furn":
                    if (bag == null || FurnitureCatalog.Get(id) == null) return null;
                    m.Have = bag.FurnitureCount(id);
                    m.Name = FurnitureCatalog.Get(id).Name;
                    unit = new CourierItem { Kind = CourierItemKind.Furniture, Id = id, Count = 1 };
                    break;
                default:
                    return null;
            }
            m.UnitKg = CourierItemKg(unit);
            return m;
        }

        /// <summary>从原处拿出 amount 装上去 <see cref="TruckDest"/> 的车（运费已经在出发时付过）。</summary>
        private bool ShipFromHere(string key, double amount)
        {
            MovableItem m = DescribeMove(key, out CourierItem unit);
            if (m == null || amount <= 1e-9) return false;
            if (m.Have + 1e-9 < amount) return Fail($"{m.Where}的{m.Name}没有这么多了。");
            string src = key.Split('|')[0];
            var items = new List<CourierItem>();
            int n = (int)Math.Round(amount);
            Pantry food = FoodOf(src);
            Backpack bag = BagOf(src);
            Stock parts = PartsOf(src);
            switch (unit.Kind)
            {
                case CourierItemKind.Part:
                    PartDef p = PartCatalog.Get(unit.Id);
                    parts.TryRemove(p, unit.Boxed, n);
                    items.Add(new CourierItem { Kind = unit.Kind, Id = unit.Id, Boxed = unit.Boxed, Count = n });
                    break;
                case CourierItemKind.Goods:
                    parts.TryRemoveGoods(unit.Id, n);
                    items.Add(new CourierItem { Kind = unit.Kind, Id = unit.Id, Count = n });
                    break;
                case CourierItemKind.Rice:
                    food.RiceJin = Math.Max(0, food.RiceJin - amount);
                    items.Add(new CourierItem { Kind = unit.Kind, Id = "rice", Jin = amount });
                    break;
                case CourierItemKind.Ingredient:
                    var taken = new List<FoodBatch>();
                    MoveBatches(food.Ingredient, taken, unit.Id, n);
                    foreach (FoodBatch b in taken)
                        items.Add(new CourierItem { Kind = unit.Kind, Id = b.Id, Count = b.Count, ExpireDay = b.ExpireDay });
                    break;
                case CourierItemKind.Gift:
                    for (int i = 0; i < n; i++) bag.TakeGift(unit.Id);
                    items.Add(new CourierItem { Kind = unit.Kind, Id = unit.Id, Count = n });
                    break;
                case CourierItemKind.Furniture:
                    bag.TakeFurniture(unit.Id, n);
                    items.Add(new CourierItem { Kind = unit.Kind, Id = unit.Id, Count = n });
                    break;
            }
            ApplyCargoLimit();
            OwnedShop dest = TruckDest;
            bool ok = true;
            foreach (CourierItem it in items)
                if (!Ship(dest, null, Money.Zero, m.Name, it, "装了"))
                {
                    PutBack(src, it);
                    ok = false;
                }
            ApplyCargoLimit();
            return ok;
        }

        private void PutBack(string src, CourierItem it)
        {
            switch (it.Kind)
            {
                case CourierItemKind.Part: PartsOf(src).ForceAdd(PartCatalog.Get(it.Id), it.Boxed, it.Count); break;
                case CourierItemKind.Goods: PartsOf(src).ForceAddGoods(it.Id, it.Count); break;
                case CourierItemKind.Rice: FoodOf(src).RiceJin += it.Jin; break;
                case CourierItemKind.Ingredient:
                    FoodOf(src).Ingredient.Add(new FoodBatch { Id = it.Id, Name = Ingredients.Get(it.Id)?.Name ?? it.Id, Count = it.Count, ExpireDay = it.ExpireDay });
                    break;
                case CourierItemKind.Gift: BagOf(src).AddGift(it.Id, it.Count); break;
                case CourierItemKind.Furniture: BagOf(src).AddFurniture(it.Id, it.Count); break;
            }
        }
    }
}
