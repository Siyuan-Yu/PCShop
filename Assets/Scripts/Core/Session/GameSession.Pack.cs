using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Inventory;

namespace PCShopSell.Core.Session
{
    /// <summary>智能手机（网购、1788 要用）、背包的承重、烟酒茶行和手机店。</summary>
    public sealed partial class GameSession
    {
        public const string PhoneBusiness = "手机";
        public static readonly Money PhonePrice = Money.FromYuan(800);
        public const string NoPhoneMessage = "你还没有智能手机，上不了网。去街上的手机店买一部。";

        /// <summary>有没有智能手机：没有就用不了网购和 1788。新游戏开局没有。</summary>
        public bool HasSmartphone { get; private set; } = true;

        /// <summary>背包：刚买的米菜、礼品。</summary>
        public Backpack Backpack { get; } = new Backpack();

        private readonly HashSet<string> _apps = new HashSet<string>(PhoneApps.All.Select(a => a.Id));

        /// <summary>手机里装了的 app。</summary>
        public IReadOnlyCollection<string> InstalledApps => _apps;

        /// <summary>作弊 / 调试：直接拥有（或取消）智能手机；拥有时所有 app 都已装好。</summary>
        public void SetSmartphone(bool has)
        {
            HasSmartphone = has;
            _apps.Clear();
            if (has)
                foreach (PhoneAppDef a in PhoneApps.All) _apps.Add(a.Id);
        }

        public bool HasApp(string id) => HasSmartphone && (_apps.Contains(id) || PhoneApps.Preinstalled.Contains(id));

        /// <summary>智能手机自带真正的日历（有提醒）；没有智能手机就只有手写的笔记本。</summary>
        public bool HasCalendar => HasSmartphone;

        /// <summary>要用某个 app 但用不了的原因；能用返回 null。</summary>
        public string AppBlocker(string id)
        {
            if (!HasSmartphone) return NoPhoneMessage;
            if (!_apps.Contains(id) && !PhoneApps.Preinstalled.Contains(id)) return $"手机上没装「{PhoneApps.Get(id)?.Name ?? id}」，去应用商店下载。";
            return null;
        }

        /// <summary>在应用商店下载 app（免费）。</summary>
        public bool InstallApp(string id)
        {
            if (!HasSmartphone) return Fail(NoPhoneMessage);
            if (PhoneApps.Get(id) == null) return false;
            if (!_apps.Contains(PhoneApps.Store)) return Fail("应用商店不见了。");
            if (_apps.Contains(id)) return Fail("已经装过了。");
            _apps.Add(id);
            Say($"下载安装了「{PhoneApps.Get(id).Name}」。");
            return true;
        }

        /// <summary>卸载（应用商店、通讯录、日历不能卸）。</summary>
        public bool UninstallApp(string id)
        {
            if (PhoneApps.Preinstalled.Contains(id)) return Fail("系统自带的，卸不了。");
            return _apps.Remove(id);
        }

        public bool BuySmartphone()
        {
            if (CurrentPlace == null || CurrentPlace.Business != PhoneBusiness) return Fail("要去手机店才能买。");
            if (HasSmartphone) return Fail("你已经有智能手机了。");
            if (!Ledger.TryDebit(Clock.Today, "购物", PhonePrice, "智能手机")) return Fail("钱不够。");
            HasSmartphone = true;
            _apps.Clear();
            foreach (string id in PhoneApps.Preinstalled) _apps.Add(id);
            Say("买了一部智能手机。里面只有应用商店、通讯录和日历，要网购、上 1788 先去应用商店下载 app。");
            return true;
        }

        // ---------- 家里的纸箱 ----------

        /// <summary>纸箱最多放多少公斤。</summary>
        public const double HomeBoxLimitKg = 20;

        /// <summary>家里的纸箱：背包里能装的东西（米菜、饭盒、礼品、家具）都能放，只看重量；吃的照样隔天会坏。</summary>
        public Backpack HomeBox { get; } = new Backpack();

        /// <summary>放在纸箱里的配件。</summary>
        public Stock HomeBoxParts { get; } = new Stock(double.MaxValue);

        public IEnumerable<KeyValuePair<string, int>> HomeBoxLines => HomeBox.GiftLines;

        public int HomeBoxCount(string giftId) => HomeBox.GiftCount(giftId);

        public double HomeBoxKg => HomeBox.WeightKg + HomeBoxParts.UsedSpace * Backpack.KgPerSpace;

        private bool AtHome => CurrentPlace != null && CurrentPlace == Home;

        // ---------- 店里的储物柜 ----------

        /// <summary>店里储物柜最多放多少公斤（配件不算，配件直接上货架）。</summary>
        public const double ShopLockerLimitKg = 40;

        /// <summary>店里柜台下的储物柜：吃的、礼品、小家具都能放；配件放进来就直接上货架。吃的照样隔天会坏。</summary>
        public Backpack ShopLocker { get; } = new Backpack();

        /// <summary>现在站的地方的储物处：家里是纸箱，店里是储物柜；别处为 null。</summary>
        public string StorageName => AtHome ? "纸箱" : InPlayerShop ? "储物柜" : null;

        /// <summary>
        /// 把背包里的东西放进储物处（家里纸箱 / 店里储物柜）。key 和背包格子一样："part:id:b|l"、"rice"（count 按斤）、"ing:id"、"lunch"、"gift:id"、"furn:id"；
        /// 只写礼品 id 也行。店里放配件就是摆上货架。
        /// </summary>
        public bool StoreInBox(string key, int count = 1) => MoveBox(key, count, true);

        /// <summary>从储物处拿东西放进背包（不能超过出行方式的承重）。</summary>
        public bool TakeFromBox(string key, int count = 1) => MoveBox(key, count, false);

        private bool MoveBox(string key, int count, bool intoBox)
        {
            if (key == null || count <= 0) return false;
            bool shop = InPlayerShop;
            if (!AtHome && !shop) return Fail("要在家里或自己的店里才能放东西。");
            string storeName = shop ? "储物柜" : "纸箱";
            Backpack store = shop ? ShopLocker : HomeBox;
            Stock storeParts = shop ? PlayerShop.Stock : HomeBoxParts;
            double limit = shop ? ShopLockerLimitKg : HomeBoxLimitKg;
            double storeKg = shop ? ShopLocker.WeightKg : HomeBoxKg;
            if (!key.Contains(":") && key != "rice" && key != "lunch") key = "gift:" + key;
            Backpack from = intoBox ? Backpack : store, to = intoBox ? store : Backpack;
            Stock fromParts = intoBox ? Cargo : storeParts, toParts = intoBox ? storeParts : Cargo;
            string[] k = key.Split(':');
            string id = k.Length > 1 ? k[1] : null;
            double have, unitKg;
            string name;
            PartDef part = null;
            bool boxed = false;
            switch (k[0])
            {
                case "part":
                    if (!PartCatalog.Exists(id)) return false;
                    part = PartCatalog.Get(id);
                    boxed = k.Length > 2 && k[2] == "b";
                    have = fromParts.Count(part, boxed);
                    unitKg = Stock.SpaceOf(part, boxed, 1) * Backpack.KgPerSpace;
                    name = part.Name + (boxed ? "（整盒）" : "（散件）");
                    break;
                case "rice":
                    have = from.Food.RiceJin;
                    unitKg = Backpack.RiceKgPerJin;
                    name = "大米";
                    break;
                case "ing":
                    have = from.Food.IngredientCount(id);
                    unitKg = Backpack.IngredientKg(id);
                    name = Ingredients.Get(id)?.Name ?? id;
                    break;
                case "lunch":
                    have = from.Food.PackedBowls;
                    unitKg = Backpack.LunchboxKg;
                    name = "饭盒";
                    break;
                case "gift":
                    GiftDef g = Gifts.Get(id);
                    if (g == null) return false;
                    have = from.GiftCount(id);
                    unitKg = g.WeightKg;
                    name = g.Name;
                    break;
                case "furn":
                    FurnitureDef f = FurnitureCatalog.Get(id);
                    if (f == null) return false;
                    have = from.FurnitureCount(id);
                    unitKg = f.WeightKg;
                    name = f.Name;
                    break;
                default:
                    return false;
            }
            bool rice = k[0] == "rice";
            double amount = rice ? Math.Min(count, have) : count;
            if (have <= 1e-9 || (!rice && have < count)) return Fail(intoBox ? "背包里没有这么多。" : $"{storeName}里没有这么多。");
            double kg = amount * unitKg;
            if (intoBox && shop && part != null)
            {
                if (PlayerShop.Stock.FreeSpace + 1e-9 < Stock.SpaceOf(part, boxed, (int)Math.Round(amount)))
                    return Fail("店里货架放不下了（拆掉包装能省地方）。");
            }
            else if (intoBox && storeKg + kg > limit + 1e-9)
                return Fail($"{storeName}放不下了：最多 {limit:0.#} 公斤，已经 {storeKg:0.#} 公斤，这些要 {kg:0.#} 公斤。");
            if (!intoBox && !CanCarry(kg)) return false;

            int n = (int)Math.Round(amount);
            switch (k[0])
            {
                case "part":
                    fromParts.TryRemove(part, boxed, n);
                    toParts.ForceAdd(part, boxed, n);
                    break;
                case "rice":
                    from.Food.RiceJin -= amount;
                    if (from.Food.RiceJin < 1e-9) from.Food.RiceJin = 0;
                    to.Food.RiceJin += amount;
                    break;
                case "ing":
                    MoveBatches(from.Food.Ingredient, to.Food.Ingredient, id, n);
                    break;
                case "lunch":
                    MoveBatches(from.Food.Packed, to.Food.Packed, null, n);
                    break;
                case "gift":
                    for (int i = 0; i < n; i++) from.TakeGift(id);
                    to.AddGift(id, n);
                    break;
                case "furn":
                    from.TakeFurniture(id, n);
                    to.AddFurniture(id, n);
                    break;
            }
            ApplyCargoLimit();
            string qty = rice ? $" {amount:0.#} 斤" : n > 1 ? "×" + n : "";
            string where = shop && part != null ? "货架上" : storeName + "里";
            Say(intoBox ? $"把「{name}」{qty}放到了{where}。" : $"从{where}拿了「{name}」{qty}放进背包。");
            return true;
        }

        /// <summary>挪一批吃的（先挪快过期的），保留各自的过期日。id 为 null 表示不分种类。</summary>
        private static void MoveBatches(List<FoodBatch> from, List<FoodBatch> to, string id, int count)
        {
            foreach (FoodBatch b in from.Where(x => id == null || x.Id == id).OrderBy(x => x.ExpireDay).ToList())
            {
                if (count <= 0) break;
                int take = Math.Min(count, b.Count);
                FoodBatch moved = b.Clone();
                moved.Count = take;
                to.Add(moved);
                b.Count -= take;
                count -= take;
                if (b.Count <= 0) from.Remove(b);
            }
        }

        // ---------- 承重 ----------

        /// <summary>现在的出行方式最多能背 / 载多少公斤。</summary>
        public double CarryLimitKg => CurrentVehicle.CargoCapacity * Backpack.KgPerSpace;

        /// <summary>现在身上的货（进货的配件 + 背包里的东西）合计多少公斤。</summary>
        public double CarriedKg => Cargo.UsedSpace * Backpack.KgPerSpace + Backpack.WeightKg;

        public double FreeKg => Math.Max(0, CarryLimitKg - CarriedKg);

        /// <summary>背包里的东西占了载货的一部分，进货能装的格子相应少一点。</summary>
        public void ApplyCargoLimit()
        {
            Cargo.Capacity = Math.Max(0, CurrentVehicle.CargoCapacity - Backpack.Space);
        }

        private bool CanCarry(double kg)
        {
            if (kg <= FreeKg + 1e-9) return true;
            return Fail($"拿不动了：再背 {kg:0.#} 公斤会超重（{CurrentVehicle.Name}最多 {CarryLimitKg:0.#} 公斤，已经 {CarriedKg:0.#} 公斤）。");
        }

        // ---------- 买礼品 ----------

        private bool AtBusiness(string business) => CurrentPlace != null && CurrentPlace.Business == business;

        /// <summary>在烟酒茶行 / 农贸市场买礼品：装进背包，或者叫车送回家（放进纸箱）。</summary>
        public bool BuyGift(string id, int count = 1)
        {
            GiftDef g = Gifts.Get(id);
            if (g == null || count <= 0) return false;
            if (!AtBusiness(g.SoldAt)) return Fail(g.SoldAt == Gifts.FarmBusiness ? "水果要去农贸市场买。" : "要去烟酒茶行买。");
            if (g.SoldAt == FarmBusiness && !IsFarmMarketOpen) return Fail("农贸市场收摊了（6:00–18:00）。");
            string what = g.Name + (count > 1 ? "×" + count : "");
            if (SendByCourier)
                return PayAndSend("人情", g.Price * count, what, false, new CourierItem { Kind = CourierItemKind.Gift, Id = id, Count = count });
            if (!CanCarry(g.WeightKg * count)) return false;
            if (!Ledger.TryDebit(Clock.Today, "人情", g.Price * count, what)) return Fail("钱不够。");
            Backpack.AddGift(id, count);
            ApplyCargoLimit();
            Say($"买了「{g.Name}」放进背包。");
            return true;
        }

        // ---------- 在自己的地盘把背包里的东西放下 ----------

        /// <summary>现在能把背包里的东西放到哪：“店里” / “家里”；不在自己的地盘返回 null。</summary>
        public string UnloadTarget => InPlayerShop ? "店里" : AtHome ? "家里" : null;

        /// <summary>现在站的地方能放下的东西有没有（没有就不显示按钮）。</summary>
        public bool HasUnloadable
        {
            get
            {
                bool things = Backpack.Food.RiceJin > 0 || Backpack.Food.Ingredient.Count > 0 || Backpack.GiftLines.Any() || Backpack.FurnitureLines.Any();
                if (InPlayerShop) return !Cargo.IsEmpty || things;
                if (AtHome) return things;
                return false;
            }
        }

        /// <summary>把背包里的米、菜、礼品放进店里储物柜，到上限为止。返回放了几样、有没有剩下。</summary>
        private int MoveFoodAndGiftsToLocker(out bool full)
        {
            full = false;
            int moved = 0;
            Pantry food = Backpack.Food;
            double Room() => ShopLockerLimitKg - ShopLocker.WeightKg;
            if (food.RiceJin > 0)
            {
                double jin = Math.Min(food.RiceJin, Math.Floor(Room() / Backpack.RiceKgPerJin * 2 + 1e-9) / 2);
                if (jin < food.RiceJin - 1e-9) full = true;
                if (jin > 0)
                {
                    food.RiceJin -= jin;
                    if (food.RiceJin < 1e-9) food.RiceJin = 0;
                    ShopLocker.Food.RiceJin += jin;
                    moved++;
                }
            }
            foreach (string id in food.Ingredient.Select(b => b.Id).Distinct().ToList())
            {
                int have = food.IngredientCount(id);
                int n = Math.Min(have, (int)Math.Floor(Room() / Backpack.IngredientKg(id) + 1e-9));
                if (n < have) full = true;
                if (n <= 0) continue;
                MoveBatches(food.Ingredient, ShopLocker.Food.Ingredient, id, n);
                moved++;
            }
            foreach (var kv in Backpack.GiftLines.ToList())
            {
                GiftDef g = Gifts.Get(kv.Key);
                int n = Math.Min(kv.Value, (int)Math.Floor(Room() / g.WeightKg + 1e-9));
                if (n < kv.Value) full = true;
                if (n <= 0) continue;
                for (int i = 0; i < n; i++) Backpack.TakeGift(kv.Key);
                ShopLocker.AddGift(kv.Key, n);
                moved++;
            }
            return moved;
        }

        /// <summary>
        /// 一键放下：在店里，进货的配件上货架（放不下的留在身上），米菜礼品进店里储物柜，家具进家具背包；
        /// 在家里，米和菜进厨房，礼品进纸箱（装不下的留在背包），家具进家具背包。饭盒一直带在身上。
        /// </summary>
        public bool UnloadBackpack()
        {
            if (UnloadTarget == null) return Fail("要在自己的店里或家里才能把东西放下。");
            var done = new List<string>();
            if (InPlayerShop)
            {
                int moved = MoveCargoToShop(out bool leftover);
                if (moved > 0) done.Add($"配件 {moved} 件上了货架");
                if (leftover) done.Add("店里放不下了，剩下的配件还在身上（拆掉包装能省地方）");
                if (MoveFoodAndGiftsToLocker(out bool lockerFull) > 0) done.Add("吃的、礼品放进了店里储物柜");
                if (lockerFull) done.Add($"储物柜满了（{ShopLockerLimitKg:0} 公斤），剩下的还在背包里");
            }
            else
            {
                Pantry food = Backpack.Food;
                if (food.RiceJin > 0 || food.Ingredient.Count > 0)
                {
                    Pantry.RiceJin += food.RiceJin;
                    food.RiceJin = 0;
                    foreach (FoodBatch b in food.Ingredient) Pantry.Ingredient.Add(b);
                    food.Ingredient.Clear();
                    done.Add("米和菜放进了厨房");
                }
                int boxed = 0;
                bool full = false;
                foreach (var kv in Backpack.GiftLines.ToList())
                {
                    GiftDef g = Gifts.Get(kv.Key);
                    int room = (int)Math.Floor((HomeBoxLimitKg - HomeBoxKg) / g.WeightKg + 1e-9);
                    int n = Math.Min(kv.Value, Math.Max(0, room));
                    if (n < kv.Value) full = true;
                    if (n <= 0) continue;
                    for (int i = 0; i < n; i++) Backpack.TakeGift(kv.Key);
                    HomeBox.AddGift(kv.Key, n);
                    boxed += n;
                }
                if (boxed > 0) done.Add($"礼品 {boxed} 份放进纸箱");
                if (full) done.Add("纸箱满了，剩下的礼品还在背包里");
            }
            var furn = Backpack.FurnitureLines.ToList();
            if (furn.Count > 0)
            {
                foreach (var kv in furn) AddFurniture(kv.Key, kv.Value);
                Backpack.ClearFurniture();
                done.Add("家具放进了家具背包（点“我的”→摆放家具）");
            }
            ApplyCargoLimit();
            if (done.Count == 0) return Fail(InPlayerShop ? "背包里没有能放下的东西（饭盒一直带在身上）。" : "背包里没有能放在家里的东西（配件要拿到店里）。");
            Say("放下了：" + string.Join("；", done) + "。");
            return true;
        }

        /// <summary>背包里的菜隔天也会坏。</summary>
        private void SpoilBackpackFood(int today)
        {
            List<string> lost = Backpack.Food.Spoil(today);
            if (lost.Count > 0)
            {
                Say("背包里放坏扔掉了：" + string.Join("、", lost) + "。");
                ApplyCargoLimit();
            }
        }
    }
}
