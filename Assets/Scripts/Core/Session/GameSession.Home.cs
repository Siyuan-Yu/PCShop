using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Shop;
using PCShopSell.Core.Time;

namespace PCShopSell.Core.Session
{
    public enum ParcelKind
    {
        Furniture,
        Outfit,
        Rice,
        Gift,
    }

    /// <summary>网购的快递，到了日子送进家里。</summary>
    public sealed class Parcel
    {
        public ParcelKind Kind;
        public string Id;
        public int Count;
        public int DueDay;

        /// <summary>货车送到店里（而不是家里）。</summary>
        public bool ToShop;
    }

    /// <summary>
    /// 家与店里的家具、网购快递、做饭吃饭。
    /// 买东西两条路：去家具城（当天拿，原价）或者网购（85 折，2 天后送到）。
    /// 家里的家具随时摆；店里摆家具要先歇业装修（当天剩下的时间不营业）。
    /// </summary>
    public sealed partial class GameSession
    {
        public const double OnlineDiscount = 0.85;
        public const int DeliveryDays = 2;
        public const string MallBusiness = "家具";
        public const string FarmBusiness = "农贸";
        public const int FarmOpenMinute = 6 * 60;
        public const int FarmCloseMinute = 18 * 60;
        public const double RiceBagJin = 10;
        public static readonly Money RiceBagPrice = Money.FromYuan(25);
        public const double NoBedSleepQuality = 0.6;
        public const double DecorTrafficPerPoint = 0.01;
        public const double DecorMaxPoints = 20;

        private readonly Dictionary<string, int> _furnStock = new Dictionary<string, int>();
        private readonly List<PlacedFurniture> _homeLayout = FurnitureCatalog.StartHomeLayout();
        private List<PlacedFurniture> _shopLayout => _focus.Layout;
        private readonly List<Parcel> _parcels = new List<Parcel>();
        private int _renovationDay
        {
            get => _focus.RenovationDay;
            set => _focus.RenovationDay = value;
        }

        public Pantry Pantry { get; } = new Pantry();
        public IReadOnlyList<PlacedFurniture> HomeLayout => _homeLayout;
        public IReadOnlyList<PlacedFurniture> ShopLayout => _shopLayout;
        public IReadOnlyList<Parcel> Parcels => _parcels;

        public IEnumerable<KeyValuePair<string, int>> FurnitureStock => _furnStock.Where(kv => kv.Value > 0);

        public int FurnitureInStock(string id) => _furnStock.TryGetValue(FurnitureCatalog.Canonical(id), out int n) ? n : 0;

        private Backpack BoxOf(bool shop) => shop ? ShopLocker : HomeBox;
        private Backpack TempOf(bool shop) => shop ? ShopTemp : HomeTemp;

        /// <summary>在这间屋（家 / 当前这家店）摆放时手边能拿来摆的某件家具：临时存放 + 纸箱 + 以前收着的。</summary>
        public int FurnitureToPlace(bool shop, string id) =>
            TempOf(shop).FurnitureCount(id) + BoxOf(shop).FurnitureCount(id) + FurnitureInStock(id);

        /// <summary>在这间屋摆放时能拿来摆的全部家具（临时存放、纸箱里的和以前收着的合在一起）。</summary>
        public IEnumerable<KeyValuePair<string, int>> FurnitureToPlaceList(bool shop) =>
            TempOf(shop).FurnitureLines.Concat(BoxOf(shop).FurnitureLines).Concat(FurnitureStock)
                .GroupBy(kv => kv.Key).Select(g => new KeyValuePair<string, int>(g.Key, g.Sum(kv => kv.Value)));

        /// <summary>家具放进这间屋的纸箱，放不下的进临时存放。返回进临时存放的件数。</summary>
        private int StoreFurnitureHere(bool shop, string id, int count) =>
            StoreOrTemp(shop, FurnitureCatalog.Get(id)?.WeightKg ?? 0, count, n => BoxOf(shop).AddFurniture(id, n), n => TempOf(shop).AddFurniture(id, n));

        public RoomSpec ShopRoom => RoomSpec.ForShop(PlayerShop.Size);

        // ---------- 家里的效果 ----------

        private IEnumerable<FurnitureDef> PlacedDefs(IEnumerable<PlacedFurniture> layout) =>
            layout.Select(p => FurnitureCatalog.Get(p.Id)).Where(d => d != null);

        /// <summary>睡眠质量倍数：摆出来的最好的床 + 空调/风扇/窗帘 + 装饰分（上限 +0.08）。没有床只能睡地上。</summary>
        public double HomeSleepQuality
        {
            get
            {
                var defs = PlacedDefs(_homeLayout).ToList();
                double bed = defs.Where(d => d.Role == FurnitureRole.Bed).Select(d => d.BedQuality).DefaultIfEmpty(NoBedSleepQuality).Max();
                double bonus = defs.Sum(d => d.SleepBonus);
                double appeal = defs.Sum(d => d.Appeal);
                return bed + bonus + Math.Min(0.08, 0.01 * appeal) + HomeTier * .08;
            }
        }

        /// <summary>每月水电（摆出来的电器）。</summary>
        public Money HomeUtilities
        {
            get
            {
                Money total = Money.Zero;
                foreach (FurnitureDef d in PlacedDefs(_homeLayout)) total += d.Utilities;
                return total;
            }
        }

        public bool HomeHas(FurnitureRole role) => PlacedDefs(_homeLayout).Any(d => d.Role == role);

        public double ShopDecorAppeal => Math.Min(DecorMaxPoints, PlacedDefs(_shopLayout).Sum(d => d.Appeal));

        public int ShopDecorPatience => PlacedDefs(_shopLayout).Sum(d => d.PatienceBonus);

        public double ShopDecorTrafficMultiplier => 1.0 + DecorTrafficPerPoint * ShopDecorAppeal;

        // ---------- 买 ----------

        // 家具城的买法（购物车、结算、货车）见 GameSession.Mall.cs。

        public Money OnlinePrice(ParcelKind kind, string id)
        {
            switch (kind)
            {
                case ParcelKind.Furniture:
                    FurnitureDef f = FurnitureCatalog.Get(id);
                    return f == null ? Money.Zero : f.Price.Scale(OnlineDiscount);
                case ParcelKind.Outfit:
                    OutfitDef o = Outfits.Get(id);
                    return o == null ? Money.Zero : o.Price.Scale(OnlineDiscount);
                case ParcelKind.Gift:
                    GiftDef g = Gifts.Get(id);
                    return g == null ? Money.Zero : g.Price.Scale(OnlineDiscount);
                default:
                    return RiceBagPrice;
            }
        }

        /// <summary>网购：随时随地下单，2 天后送到家里。</summary>
        public bool OrderOnline(ParcelKind kind, string id, int count = 1)
        {
            if (IsGameOver || count <= 0) return false;
            string appBlock = AppBlocker(PhoneApps.Shop);
            if (appBlock != null) return Fail(appBlock);
            string name;
            switch (kind)
            {
                case ParcelKind.Furniture:
                    FurnitureDef f = FurnitureCatalog.Get(id);
                    if (f == null || f.Starter) return false;
                    name = f.Name;
                    break;
                case ParcelKind.Outfit:
                    OutfitDef o = Outfits.Get(id);
                    if (o == null || id == Outfits.Default) return false;
                    if (_ownedOutfits.Contains(id) || _parcels.Any(p => p.Kind == ParcelKind.Outfit && p.Id == id)) return Fail("这件已经有了（或者在路上）。");
                    name = o.Name;
                    count = 1;
                    break;
                case ParcelKind.Gift:
                    GiftDef gd = Gifts.Get(id);
                    if (gd == null) return false;
                    name = gd.Name;
                    break;
                default:
                    name = "大米 10 斤";
                    id = "rice";
                    break;
            }
            Money total = OnlinePrice(kind, id) * count;
            if (!Ledger.TryDebit(Clock.Today, "网购", total, name + (count > 1 ? "×" + count : ""))) return Fail("钱不够。");
            _parcels.Add(new Parcel { Kind = kind, Id = id, Count = count, DueDay = Clock.Today.TotalDays + DeliveryDays });
            Say($"下单了「{name}」，{DeliveryDays} 天后送到家。");
            return true;
        }

        internal void AddFurniture(string id, int count)
        {
            id = FurnitureCatalog.Canonical(id);
            _furnStock[id] = FurnitureInStock(id) + count;
        }

        private void DeliverParcels(GameDate today)
        {
            for (int i = _parcels.Count - 1; i >= 0; i--)
            {
                Parcel p = _parcels[i];
                if (p.DueDay > today.TotalDays) continue;
                _parcels.RemoveAt(i);
                string what;
                switch (p.Kind)
                {
                    case ParcelKind.Furniture:
                        bool toShop = p.ToShop && HasShop;
                        int over = 0;
                        WithShop(toShop ? MainShop : null, () => over = StoreFurnitureHere(toShop, p.Id, p.Count));
                        string box = toShop ? "店里纸箱" : "纸箱";
                        what = FurnitureCatalog.Get(p.Id).Name + (p.Count > 1 ? "×" + p.Count : "")
                            + (over <= 0 ? $"（放进了{box}）" : over >= p.Count ? $"（{box}满了，放在临时存放）" : $"（{box}放不下的 {over} 件放在临时存放）");
                        break;
                    case ParcelKind.Outfit:
                        _ownedOutfits.Add(p.Id);
                        what = Outfits.Get(p.Id).Name + "（在衣柜里换上）";
                        break;
                    case ParcelKind.Gift:
                        Backpack.AddGift(p.Id, p.Count);
                        ApplyCargoLimit();
                        what = Gifts.Get(p.Id).Name + (p.Count > 1 ? "×" + p.Count : "") + "（已放进背包）";
                        break;
                    default:
                        Pantry.RiceJin += RiceBagJin * p.Count;
                        what = $"大米 {RiceBagJin * p.Count:0} 斤";
                        break;
                }
                Popup?.Invoke(p.ToShop ? "货车到店里了" : "快递到了", (p.ToShop ? "卸在店门口，工人已经帮你搬进仓库：" : "放在门口：") + what);
            }
        }

        // ---------- 摆放 ----------

        public bool IsRenovating => _renovationDay == Clock.Today.TotalDays;

        /// <summary>店里摆家具要装修：当天剩下的时间歇业。已经在装修就直接可以。</summary>
        public bool BeginRenovation()
        {
            if (!InPlayerShop) return Fail("要在自己的店里装修。");
            if (IsRenovating) return true;
            _renovationDay = Clock.Today.TotalDays;
            PlayerShop.ForcedClosed = true;
            Say("今天歇业装修，明天恢复营业。");
            return true;
        }

        private List<PlacedFurniture> LayoutOf(bool shop) => shop ? _shopLayout : _homeLayout;

        private bool CanEdit(bool shop)
        {
            if (shop)
            {
                if (!InPlayerShop) return Fail("要在自己的店里才能摆。");
                if (!IsRenovating) return Fail("店里摆家具要先歇业装修。");
                return true;
            }
            if (CurrentPlace != Home) return Fail("要在家里才能摆。");
            return true;
        }

        public string PlacementError(bool shop, FurnitureDef def, PlacedFurniture cand, int ignore, IReadOnlyList<RectD> extra = null)
        {
            RoomSpec room = shop ? ShopRoom : RoomSpec.Home;
            return RoomPlanner.Check(room, LayoutOf(shop), def, cand, ignore, extra);
        }

        /// <summary>把手边的家具摆出来：先用临时存放里的，再用纸箱里的，最后是以前收着的。</summary>
        public bool PlaceFromStock(bool shop, string id, PlacedFurniture at, IReadOnlyList<RectD> extra = null)
        {
            id = FurnitureCatalog.Canonical(id);
            FurnitureDef def = FurnitureCatalog.Get(id);
            if (def == null || FurnitureToPlace(shop, id) <= 0) return false;
            if (!CanEdit(shop)) return false;
            string err = PlacementError(shop, def, at, -1, extra);
            if (err != null) return Fail(err);
            RememberLayout(shop);
            if (!TempOf(shop).TakeFurniture(id, 1) && !BoxOf(shop).TakeFurniture(id, 1)) _furnStock[id] = FurnitureInStock(id) - 1;
            TrackTempClocks();
            LayoutOf(shop).Add(new PlacedFurniture { Id = id, X = at.X, Z = at.Z, Rot = at.Rot });
            if (shop) ApplyUpgrades();
            return true;
        }

        public bool MoveFurniture(bool shop, int index, PlacedFurniture at, IReadOnlyList<RectD> extra = null)
        {
            List<PlacedFurniture> l = LayoutOf(shop);
            if (index < 0 || index >= l.Count || !CanEdit(shop)) return false;
            FurnitureDef def = FurnitureCatalog.Get(l[index].Id);
            string err = PlacementError(shop, def, at, index, extra);
            if (err != null) return Fail(err);
            RememberLayout(shop);
            l[index].X = at.X;
            l[index].Z = at.Z;
            l[index].Rot = at.Rot;
            return true;
        }

        /// <summary>把摆出来的家具收起来：放进这间屋的纸箱，放不下的进临时存放。</summary>
        public bool StoreFurniture(bool shop, int index)
        {
            List<PlacedFurniture> l = LayoutOf(shop);
            if (index < 0 || index >= l.Count || !CanEdit(shop)) return false;
            if (FurnitureCatalog.Get(l[index].Id)?.Role == FurnitureRole.Storage)
            {
                double used = shop ? ShopLocker.WeightKg : HomeBoxKg;
                double after = (shop ? ShopLockerLimitKg : HomeBoxLimitKg) - BoxCapacityKg;
                if (used > after + 1e-9) return Fail($"纸箱里还放着 {used:0.#} 公斤东西，收走这个纸箱就只能放 {Math.Max(0, after):0.#} 公斤，先拿出来一些。");
            }
            RememberLayout(shop);
            string id = l[index].Id;
            l.RemoveAt(index);
            StoreFurnitureHere(shop, id, 1);
            TrackTempClocks();
            if (shop) ApplyUpgrades();
            return true;
        }

        // ---------- 农贸市场 ----------

        public bool IsFarmMarketOpen => Clock.MinuteOfDay >= FarmOpenMinute && Clock.MinuteOfDay < FarmCloseMinute;

        private bool AtFarmMarket => CurrentPlace != null && CurrentPlace.Business == FarmBusiness;

        public bool BuyRice(double jin)
        {
            if (!AtFarmMarket) return Fail("要去农贸市场买。");
            if (!IsFarmMarketOpen) return Fail("农贸市场收摊了（6:00–18:00）。");
            if (jin <= 0) return false;
            Money cost = Pantry.RicePricePerJin.Scale(jin);
            if (SendByCourier)
                return PayAndSend("买菜", cost, $"大米 {jin:0.#} 斤", false, new CourierItem { Kind = CourierItemKind.Rice, Id = "rice", Jin = jin });
            if (!CanCarry(jin * Backpack.RiceKgPerJin)) return false;
            if (!Ledger.TryDebit(Clock.Today, "买菜", cost, $"大米 {jin:0.#} 斤")) return Fail("钱不够。");
            Backpack.Food.RiceJin += jin;
            ApplyCargoLimit();
            return true;
        }

        public bool BuyIngredient(string id, int count)
        {
            IngredientDef d = Ingredients.Get(id);
            if (d == null || count <= 0) return false;
            if (!AtFarmMarket) return Fail("要去农贸市场买。");
            if (!IsFarmMarketOpen) return Fail("农贸市场收摊了（6:00–18:00）。");
            if (SendByCourier)
                return PayAndSend("买菜", d.Price * count, $"{d.Name}×{count}", false,
                    new CourierItem { Kind = CourierItemKind.Ingredient, Id = id, Count = count, ExpireDay = Clock.Today.TotalDays + d.ShelfDays });
            if (!CanCarry(Backpack.IngredientKg(id) * count)) return false;
            if (!Ledger.TryDebit(Clock.Today, "买菜", d.Price * count, $"{d.Name}×{count}")) return Fail("钱不够。");
            Backpack.Food.AddIngredient(id, count, Clock.Today.TotalDays);
            ApplyCargoLimit();
            return true;
        }

        // ---------- 做饭、吃饭 ----------

        /// <summary>用电饭锅开始焖一锅饭（要有小桌上的电饭锅）。点了就开始，40 分钟后才好，期间锅里不能动；人可以去干别的。</summary>
        public bool CookRice(double jin, string ingredientId)
        {
            if (CurrentPlace != Home) return Fail("要在家里做饭。");
            if (!HomeHas(FurnitureRole.Cooker)) return Fail("屋里没摆电饭锅。");
            if (!Pantry.StartCook(jin, ingredientId, Clock.Now.TotalMinutes, out string err)) return Fail(err);
            Say($"开始焖饭了，大约 {Pantry.CookMinutes} 分钟后好。");
            return true;
        }

        /// <summary>焖饭到点了：饭放进“做好的饭”，等人来取。</summary>
        public void RefreshCooking() => UpdateCooking(Clock.Now);

        private void UpdateCooking(GameTimestamp now)
        {
            FoodBatch b = Pantry.FinishCook(now.TotalMinutes);
            if (b != null) Say($"电饭锅跳了：{b.Name}×{b.Count} 碗焖好了。没有冰箱，放不过明天。");
        }

        /// <summary>在家吃一碗（10 分钟）。</summary>
        public bool EatBowlAtHome()
        {
            if (CurrentPlace != Home) return Fail("要在家里吃。");
            if (Player.Hunger >= 99) return Fail("不饿。");
            FoodBatch b = Pantry.TakeBowl(Pantry.Cooked);
            if (b == null) return Fail("没有做好的饭。");
            Player.Hunger = Math.Min(100, Player.Hunger + b.Hunger);
            Player.Health = Math.Min(100, Player.Health + b.Health);
            AdvanceClock(Pantry.EatMinutes);
            return true;
        }

        /// <summary>把锅里做好的饭装进饭盒放进背包（每盒 0.5 公斤，算负重），饭点从背包里拿出来吃。</summary>
        public bool PackLunch(int bowls)
        {
            if (CurrentPlace != Home) return Fail("要在家里装饭盒。");
            if (Pantry.CookedBowls == 0) return Fail("没有做好的饭。");
            int fit = (int)Math.Floor(FreeKg / Backpack.LunchboxKg + 1e-9);
            if (fit <= 0) return CanCarry(Backpack.LunchboxKg);
            int moved = 0;
            while (moved < Math.Min(bowls, fit))
            {
                FoodBatch b = Pantry.TakeBowl(Pantry.Cooked);
                if (b == null) break;
                Backpack.Food.Packed.Add(b);
                moved++;
            }
            ApplyCargoLimit();
            Say($"装了 {moved} 个饭盒放进背包。" + (moved < bowls && Pantry.CookedBowls > 0 ? "背包装不下了，剩下的还在锅里。" : ""));
            return true;
        }

        /// <summary>一顿最多吃到饱多少。</summary>
        public const double MealGain = 60;

        private double EatFrom(System.Collections.Generic.List<FoodBatch> list)
        {
            double gain = 0;
            while (gain < MealGain && Player.Hunger + gain < 100)
            {
                FoodBatch b = Pantry.TakeBowl(list);
                if (b == null) break;
                gain += b.Hunger;
                Player.Health = Math.Min(100, Player.Health + b.Health);
            }
            return gain;
        }

        /// <summary>
        /// 每天饿两次（12:00、18:30）。在家且锅里有饭就吃锅里的，否则吃背包里的饭盒；都没有就只能饿着。
        /// </summary>
        public bool MealDeliveryEnabled => Flags.Contains("life:meal-delivery");
        public void ToggleMealDelivery() { if (!Flags.Remove("life:meal-delivery")) Flags.Add("life:meal-delivery"); }

        private void EatMeal(string meal)
        {
            if (Player.Hunger >= 90) return;
            if (CurrentPlace == Home && Pantry.CookedBowls > 0)
            {
                Player.Hunger = Math.Min(100, Player.Hunger + EatFrom(Pantry.Cooked));
                Say($"{meal}在家吃了自己做的饭。");
                return;
            }
            if (Backpack.Lunchboxes > 0)
            {
                Player.Hunger = Math.Min(100, Player.Hunger + EatFrom(Backpack.Food.Packed));
                ApplyCargoLimit();
                Say($"{meal}吃了背包里的饭盒，还剩 {Backpack.Lunchboxes} 盒。");
                return;
            }
            if (MealDeliveryEnabled && HasSmartphone && Ledger.TryDebit(Clock.Today, "餐饮", Money.FromYuan(25), meal + "送餐"))
            {
                Player.Hunger = Math.Min(100, Player.Hunger + 65);
                Say($"{meal}送餐到了，花费 ¥25。优先吃自带饭，没有才送餐。");
                return;
            }
            Say($"该吃{meal}了，可背包里没有饭盒……饿着。回家焖饭，装几盒饭带在身上。");
        }

        /// <summary>饿了随时从背包里拿一个饭盒吃（10 分钟）。</summary>
        public bool EatLunchboxNow()
        {
            if (Player.Hunger >= 95) return Fail("不饿。");
            FoodBatch b = Pantry.TakeBowl(Backpack.Food.Packed);
            if (b == null) return Fail("背包里没有饭盒。");
            Player.Hunger = Math.Min(100, Player.Hunger + b.Hunger);
            Player.Health = Math.Min(100, Player.Health + b.Health);
            ApplyCargoLimit();
            AdvanceClock(Pantry.EatMinutes);
            Say($"吃了一盒{b.Name}，还剩 {Backpack.Lunchboxes} 盒。");
            return true;
        }

        private void OnHomeDayAdvanced(GameDate today)
        {
            DeliverParcels(today);
            List<string> lost = Pantry.Spoil(today.TotalDays);
            if (lost.Count > 0) Say("放坏扔掉了：" + string.Join("、", lost) + "。");
            foreach (OwnedShop s in _shops)
                if (s.RenovationDay != today.TotalDays) s.Sim.ForcedClosed = false;
            SpoilBackpackFood(today.TotalDays);
            List<string> boxLost = HomeBox.Food.Spoil(today.TotalDays);
            if (boxLost.Count > 0) Say("纸箱里放坏扔掉了：" + string.Join("、", boxLost) + "。");
            List<string> tempLost = HomeTemp.Food.Spoil(today.TotalDays);
            if (tempLost.Count > 0) Say("家里临时存放的放坏扔掉了：" + string.Join("、", tempLost) + "。");
            foreach (OwnedShop s in _shops)
            {
                List<string> lockerLost = s.Locker.Food.Spoil(today.TotalDays).Concat(s.Temp.Food.Spoil(today.TotalDays)).ToList();
                if (lockerLost.Count > 0) Say($"{(MyShopCount > 1 ? ShopTitle(s) : "店里")}纸箱里放坏扔掉了：" + string.Join("、", lockerLost) + "。");
            }
        }

        private static SaveStorage ToSaveStorage(Backpack b, Stock parts = null) => new SaveStorage
        {
            parts = parts == null ? null : Lines(parts),
            giftIds = b.GiftLines.Select(kv => kv.Key).ToArray(),
            giftCounts = b.GiftLines.Select(kv => kv.Value).ToArray(),
            rice = b.Food.RiceJin,
            ingredients = ToSave(b.Food.Ingredient),
            lunch = ToSave(b.Food.Packed),
            furnIds = b.FurnitureLines.Select(kv => kv.Key).ToArray(),
            furnCounts = b.FurnitureLines.Select(kv => kv.Value).ToArray(),
        };

        private static void FromSaveStorage(SaveStorage s, Backpack b, Stock parts = null)
        {
            b.Food.RiceJin = 0;
            b.Food.Ingredient.Clear();
            b.Food.Packed.Clear();
            b.ClearGifts();
            b.ClearFurniture();
            parts?.Clear();
            if (s == null) return;
            if (parts != null) Fill(parts, s.parts);
            string[] g = s.giftIds ?? Array.Empty<string>();
            for (int i = 0; i < g.Length; i++)
                if (Gifts.Get(g[i]) != null && s.giftCounts != null && i < s.giftCounts.Length) b.AddGift(g[i], s.giftCounts[i]);
            b.Food.RiceJin = s.rice;
            FromSave(s.ingredients, b.Food.Ingredient);
            FromSave(s.lunch, b.Food.Packed);
            string[] f = s.furnIds ?? Array.Empty<string>();
            for (int i = 0; i < f.Length; i++)
                if (FurnitureCatalog.Get(f[i]) != null && s.furnCounts != null && i < s.furnCounts.Length) b.AddFurniture(f[i], s.furnCounts[i]);
        }

        // ---------- 存档 ----------

        private static SavePlaced[] ToSave(IEnumerable<PlacedFurniture> l) =>
            l.Select(p => new SavePlaced { id = p.Id, x = p.X, z = p.Z, rot = p.Rot, item = p.Item }).ToArray();

        /// <summary>读布局：老存档里按类别分的货架（shelf_gpu……）变成设好类别的通用货架，老杂货架变成没设置的货架。</summary>
        private static List<PlacedFurniture> FromSave(SavePlaced[] a) =>
            (a ?? Array.Empty<SavePlaced>()).Where(p => FurnitureCatalog.Get(p.id) != null)
                .Select(p => new PlacedFurniture
                {
                    Id = FurnitureCatalog.Canonical(p.id), X = p.x, Z = p.z, Rot = p.rot,
                    Item = ShelfKinds.Get(p.item) != null ? p.item : FurnitureCatalog.LegacyShelfKind(p.id),
                }).ToList();

        private static SaveBatch[] ToSave(IEnumerable<FoodBatch> l) =>
            l.Select(b => new SaveBatch { id = b.Id, name = b.Name, hunger = b.Hunger, health = b.Health, count = b.Count, expireDay = b.ExpireDay }).ToArray();

        private static void FromSave(SaveBatch[] a, List<FoodBatch> into)
        {
            into.Clear();
            foreach (SaveBatch b in a ?? Array.Empty<SaveBatch>())
                into.Add(new FoodBatch { Id = b.id, Name = b.name, Hunger = b.hunger, Health = b.health, Count = b.count, ExpireDay = b.expireDay });
        }

        private void SaveHomeData(SaveData d)
        {
            d.home = new SaveHome
            {
                saved = true,
                fixturesV = 1,
                stockIds = _furnStock.Where(kv => kv.Value > 0).Select(kv => kv.Key).ToArray(),
                stockCounts = _furnStock.Where(kv => kv.Value > 0).Select(kv => kv.Value).ToArray(),
                homeLayout = ToSave(_homeLayout),
                shopLayout = ToSave(_shopLayout),
                parcels = _parcels.Select(p => new SaveParcel { kind = (int)p.Kind, id = p.Id, count = p.Count, dueDay = p.DueDay, toShop = p.ToShop }).ToArray(),
                riceJin = Pantry.RiceJin,
                ingredients = ToSave(Pantry.Ingredient),
                cooked = ToSave(Pantry.Cooked),
                cooking = Pantry.Cooking == null ? null : ToSave(new[] { Pantry.Cooking.Batch }),
                cookReadyMinute = Pantry.Cooking == null ? 0 : Pantry.Cooking.ReadyMinute,
                packLunch = ToSave(Backpack.Food.Packed),
                couriers = SaveCouriers(),
                sendByCourier = SendByCourier,
                renovationDay = _renovationDay,
                visitTargets = _visits.Select(v => v.TargetId).ToArray(),
                visitStarts = _visits.Select(v => v.StartMinute).ToArray(),
                visitPrices = _visits.Select(v => v.Price.Cents).ToArray(),
                visitWorkStarts = _visits.Select(v => v.WorkStart).ToArray(),
                packRice = Backpack.Food.RiceJin,
                packIngredients = ToSave(Backpack.Food.Ingredient),
                packFurnIds = Backpack.FurnitureLines.Select(kv => kv.Key).ToArray(),
                packFurnCounts = Backpack.FurnitureLines.Select(kv => kv.Value).ToArray(),
                giftIds = Backpack.GiftLines.Select(kv => kv.Key).ToArray(),
                giftCounts = Backpack.GiftLines.Select(kv => kv.Value).ToArray(),
                boxGiftIds = HomeBoxLines.Select(kv => kv.Key).ToArray(),
                boxGiftCounts = HomeBoxLines.Select(kv => kv.Value).ToArray(),
                boxRice = HomeBox.Food.RiceJin,
                boxIngredients = ToSave(HomeBox.Food.Ingredient),
                boxLunch = ToSave(HomeBox.Food.Packed),
                boxFurnIds = HomeBox.FurnitureLines.Select(kv => kv.Key).ToArray(),
                boxFurnCounts = HomeBox.FurnitureLines.Select(kv => kv.Value).ToArray(),
                boxPartIds = HomeBoxParts.Lines().Select(l => l.Part.Id).ToArray(),
                boxPartBoxed = HomeBoxParts.Lines().Select(l => l.Boxed).ToArray(),
                boxPartCounts = HomeBoxParts.Lines().Select(l => l.Count).ToArray(),
                mallStashIds = _mallStash.Select(l => l.Id).ToArray(),
                mallStashCounts = _mallStash.Select(l => l.Count).ToArray(),
                shopLocker = ToSaveStorage(MainShop.Locker),
                homeTemp = ToSaveStorage(HomeTemp, HomeTempParts),
                shopTemp = ToSaveStorage(MainShop.Temp),
                shopBoxesGiven = MainShop.BoxesGiven,
                homeTempSince = _homeTempSince,
                shopTempSince = MainShop.TempSince,
                courierDest = CourierDest == null ? 0 : CourierDest == CourierHome ? 1 : 2,
                courierDestShop = CourierDest == null || CourierDest == CourierHome || CourierDest == CourierCurrentShop ? null : CourierDest,
            };
            d.noPhone = !HasSmartphone;
            d.apps = _apps.ToArray();
        }

        private void RestoreHomeData(SaveData d)
        {
            SetSmartphone(!d.noPhone);
            if (HasSmartphone && d.apps != null)
            {
                _apps.Clear();
                foreach (string id in d.apps)
                    if (PhoneApps.Get(id) != null) _apps.Add(id);
            }
            Backpack.Food.RiceJin = 0;
            Backpack.Food.Ingredient.Clear();
            Backpack.Food.Packed.Clear();
            Backpack.ClearGifts();
            Backpack.ClearFurniture();
            HomeBox.Food.RiceJin = 0;
            HomeBox.Food.Ingredient.Clear();
            HomeBox.Food.Packed.Clear();
            HomeBox.ClearGifts();
            HomeBox.ClearFurniture();
            HomeBoxParts.Clear();
            _furnStock.Clear();
            _homeLayout.Clear();
            _shopLayout.Clear();
            _parcels.Clear();
            _mallStash.Clear();
            SaveHome h = d.home;
            RestoreCouriers(h?.couriers);
            SendByCourier = h != null && h.sendByCourier;
            FromSaveStorage(h?.shopLocker, MainShop.Locker);
            FromSaveStorage(h?.homeTemp, HomeTemp, HomeTempParts);
            FromSaveStorage(h?.shopTemp, MainShop.Temp);
            MainShop.BoxesGiven = h != null && h.shopBoxesGiven;
            _homeTempSince = h?.homeTempSince ?? 0;
            MainShop.TempSince = h?.shopTempSince ?? 0;
            CourierDest = h == null || h.courierDest == 0 ? null
                : h.courierDest == 1 ? CourierHome
                : string.IsNullOrEmpty(h.courierDestShop) ? CourierCurrentShop : h.courierDestShop;
            if (h == null || !h.saved)
            {
                // 老存档：按原来的住处档位把买过的东西放进背包
                _homeLayout.AddRange(FurnitureCatalog.StartHomeLayout());
                Pantry.RiceJin = Pantry.StartRiceJin;
                Pantry.Ingredient.Clear();
                Pantry.Cooked.Clear();
                Pantry.Cooking = null;
                _visits.Clear();
                Pantry.Packed.Clear();
                _renovationDay = -1;
                if (d.homeComfort >= 1) AddFurniture("bed_spring", 1);
                if (d.homeComfort >= 2) AddFurniture("aircon", 1);
                if (d.homeComfort >= 3) AddFurniture(FurnitureCatalog.Curtain, 1);
                ApplyCargoLimit();
                return;
            }
            string[] ids = h.stockIds ?? Array.Empty<string>();
            for (int i = 0; i < ids.Length; i++)
                if (FurnitureCatalog.Get(ids[i]) != null && h.stockCounts != null && i < h.stockCounts.Length) AddFurniture(ids[i], h.stockCounts[i]);
            _homeLayout.AddRange(FromSave(h.homeLayout));
            _shopLayout.AddRange(FromSave(h.shopLayout));
            foreach (SaveParcel p in h.parcels ?? Array.Empty<SaveParcel>())
                _parcels.Add(new Parcel { Kind = (ParcelKind)p.kind, Id = p.id, Count = p.count, DueDay = p.dueDay, ToShop = p.toShop });
            Pantry.RiceJin = h.riceJin;
            FromSave(h.ingredients, Pantry.Ingredient);
            FromSave(h.cooked, Pantry.Cooked);
            Pantry.Cooking = null;
            if (h.cooking != null && h.cooking.Length > 0)
            {
                var job = new List<FoodBatch>();
                FromSave(h.cooking, job);
                if (job.Count > 0) Pantry.Cooking = new CookJob { Batch = job[0], ReadyMinute = h.cookReadyMinute };
            }
            Pantry.Packed.Clear();
            FromSave(h.packLunch, Backpack.Food.Packed);
            if (h.packed != null)
                foreach (SaveBatch b in h.packed)
                    Backpack.Food.Packed.Add(new FoodBatch { Id = b.id, Name = b.name, Hunger = b.hunger, Health = b.health, Count = b.count, ExpireDay = b.expireDay });
            _renovationDay = h.renovationDay;
            _visits.Clear();
            int vn = Math.Min(h.visitTargets?.Length ?? 0, Math.Min(h.visitStarts?.Length ?? 0, h.visitPrices?.Length ?? 0));
            for (int i = 0; i < vn; i++)
                _visits.Add(new HomeVisit { TargetId = h.visitTargets[i], StartMinute = h.visitStarts[i], Price = Money.FromCents(h.visitPrices[i]), WorkStart = h.visitWorkStarts != null && i < h.visitWorkStarts.Length ? h.visitWorkStarts[i] : -1 });
            PlayerShop.ForcedClosed = _renovationDay == Clock.Today.TotalDays;
            Backpack.Food.RiceJin = h.packRice;
            FromSave(h.packIngredients, Backpack.Food.Ingredient);
            string[] gids = h.giftIds ?? Array.Empty<string>();
            for (int i = 0; i < gids.Length; i++)
                if (h.giftCounts != null && i < h.giftCounts.Length) Backpack.AddGift(gids[i], h.giftCounts[i]);
            string[] bids = h.boxGiftIds ?? Array.Empty<string>();
            for (int i = 0; i < bids.Length; i++)
                if (h.boxGiftCounts != null && i < h.boxGiftCounts.Length) HomeBox.AddGift(bids[i], h.boxGiftCounts[i]);
            HomeBox.Food.RiceJin = h.boxRice;
            FromSave(h.boxIngredients, HomeBox.Food.Ingredient);
            FromSave(h.boxLunch, HomeBox.Food.Packed);
            string[] bf = h.boxFurnIds ?? Array.Empty<string>();
            for (int i = 0; i < bf.Length; i++)
                if (h.boxFurnCounts != null && i < h.boxFurnCounts.Length) HomeBox.AddFurniture(bf[i], h.boxFurnCounts[i]);
            string[] bp = h.boxPartIds ?? Array.Empty<string>();
            for (int i = 0; i < bp.Length; i++)
                if (PartCatalog.Exists(bp[i]) && h.boxPartCounts != null && i < h.boxPartCounts.Length)
                    HomeBoxParts.ForceAdd(PartCatalog.Get(bp[i]), h.boxPartBoxed != null && i < h.boxPartBoxed.Length && h.boxPartBoxed[i], h.boxPartCounts[i]);
            string[] fids = h.packFurnIds ?? Array.Empty<string>();
            for (int i = 0; i < fids.Length; i++)
                if (h.packFurnCounts != null && i < h.packFurnCounts.Length) Backpack.AddFurniture(fids[i], h.packFurnCounts[i]);
            string[] sids = h.mallStashIds ?? Array.Empty<string>();
            for (int i = 0; i < sids.Length; i++)
                if (h.mallStashCounts != null && i < h.mallStashCounts.Length && h.mallStashCounts[i] > 0 && FurnitureCatalog.Get(sids[i]) != null) AddStash(FurnitureCatalog.Canonical(sids[i]), h.mallStashCounts[i]);
            ApplyCargoLimit();
        }
    }
}
