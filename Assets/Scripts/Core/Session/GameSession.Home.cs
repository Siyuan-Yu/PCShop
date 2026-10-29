using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
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
        private readonly List<PlacedFurniture> _shopLayout = new List<PlacedFurniture>();
        private readonly List<Parcel> _parcels = new List<Parcel>();
        private int _renovationDay = -1;

        public Pantry Pantry { get; } = new Pantry();
        public IReadOnlyList<PlacedFurniture> HomeLayout => _homeLayout;
        public IReadOnlyList<PlacedFurniture> ShopLayout => _shopLayout;
        public IReadOnlyList<Parcel> Parcels => _parcels;

        public IEnumerable<KeyValuePair<string, int>> FurnitureStock => _furnStock.Where(kv => kv.Value > 0);

        public int FurnitureInStock(string id) => _furnStock.TryGetValue(id, out int n) ? n : 0;

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
                return bed + bonus + Math.Min(0.08, 0.01 * appeal);
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

        private void AddFurniture(string id, int count) => _furnStock[id] = FurnitureInStock(id) + count;

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
                        AddFurniture(p.Id, p.Count);
                        what = FurnitureCatalog.Get(p.Id).Name + (p.Count > 1 ? "×" + p.Count : "") + "（在“我的”里摆放）";
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
                Popup?.Invoke("快递到了", "放在门口：" + what);
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
            if (def.ForShop != shop && !def.Starter) return shop ? "这件是家里用的。" : "这件是店里用的。";
            return RoomPlanner.Check(room, LayoutOf(shop), def, cand, ignore, extra);
        }

        /// <summary>把背包里的家具摆出来。</summary>
        public bool PlaceFromStock(bool shop, string id, PlacedFurniture at, IReadOnlyList<RectD> extra = null)
        {
            FurnitureDef def = FurnitureCatalog.Get(id);
            if (def == null || FurnitureInStock(id) <= 0) return false;
            if (!CanEdit(shop)) return false;
            string err = PlacementError(shop, def, at, -1, extra);
            if (err != null) return Fail(err);
            _furnStock[id] = FurnitureInStock(id) - 1;
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
            l[index].X = at.X;
            l[index].Z = at.Z;
            l[index].Rot = at.Rot;
            return true;
        }

        /// <summary>把摆出来的家具收回背包。</summary>
        public bool StoreFurniture(bool shop, int index)
        {
            List<PlacedFurniture> l = LayoutOf(shop);
            if (index < 0 || index >= l.Count || !CanEdit(shop)) return false;
            AddFurniture(l[index].Id, 1);
            l.RemoveAt(index);
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
            if (!CanCarry(jin * Backpack.RiceKgPerJin)) return false;
            Money cost = Pantry.RicePricePerJin.Scale(jin);
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

        /// <summary>把做好的饭装进饭盒带出门，饭点优先吃这个。</summary>
        public bool PackLunch(int bowls)
        {
            if (CurrentPlace != Home) return Fail("要在家里装饭盒。");
            int moved = Pantry.Pack(bowls);
            if (moved == 0) return Fail("没有做好的饭。");
            Say($"装了 {moved} 个饭盒。");
            return true;
        }

        private double EatFrom(System.Collections.Generic.List<FoodBatch> list)
        {
            double gain = 0;
            while (gain < 45)
            {
                FoodBatch b = Pantry.TakeBowl(list);
                if (b == null) break;
                gain += b.Hunger;
                Player.Health = Math.Min(100, Player.Health + b.Health);
            }
            return gain;
        }

        private void EatMeal(string meal)
        {
            if (Player.Hunger >= 90) return;
            if (Pantry.PackedBowls > 0)
            {
                Player.Hunger = Math.Min(100, Player.Hunger + EatFrom(Pantry.Packed));
                Say($"{meal}吃了自己带的饭盒。");
                return;
            }
            if (CurrentPlace == Home && Pantry.CookedBowls > 0)
            {
                Player.Hunger = Math.Min(100, Player.Hunger + EatFrom(Pantry.Cooked));
                Say($"{meal}在家吃了自己做的饭。");
                return;
            }
            if (!Ledger.TryDebit(Clock.Today, "吃饭", MealPrice, meal))
            {
                Say($"没钱吃{meal}了……");
                return;
            }
            Player.Hunger = Math.Min(100, Player.Hunger + 45);
        }

        private void OnHomeDayAdvanced(GameDate today)
        {
            DeliverParcels(today);
            List<string> lost = Pantry.Spoil(today.TotalDays);
            if (lost.Count > 0) Say("放坏扔掉了：" + string.Join("、", lost) + "。");
            if (_renovationDay != today.TotalDays) PlayerShop.ForcedClosed = false;
            SpoilBackpackFood(today.TotalDays);
        }

        // ---------- 存档 ----------

        private static SavePlaced[] ToSave(IEnumerable<PlacedFurniture> l) =>
            l.Select(p => new SavePlaced { id = p.Id, x = p.X, z = p.Z, rot = p.Rot }).ToArray();

        private static List<PlacedFurniture> FromSave(SavePlaced[] a) =>
            (a ?? Array.Empty<SavePlaced>()).Where(p => FurnitureCatalog.Get(p.id) != null)
                .Select(p => new PlacedFurniture { Id = p.id, X = p.x, Z = p.z, Rot = p.rot }).ToList();

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
                stockIds = _furnStock.Where(kv => kv.Value > 0).Select(kv => kv.Key).ToArray(),
                stockCounts = _furnStock.Where(kv => kv.Value > 0).Select(kv => kv.Value).ToArray(),
                homeLayout = ToSave(_homeLayout),
                shopLayout = ToSave(_shopLayout),
                parcels = _parcels.Select(p => new SaveParcel { kind = (int)p.Kind, id = p.Id, count = p.Count, dueDay = p.DueDay }).ToArray(),
                riceJin = Pantry.RiceJin,
                ingredients = ToSave(Pantry.Ingredient),
                cooked = ToSave(Pantry.Cooked),
                cooking = Pantry.Cooking == null ? null : ToSave(new[] { Pantry.Cooking.Batch }),
                cookReadyMinute = Pantry.Cooking == null ? 0 : Pantry.Cooking.ReadyMinute,
                packed = ToSave(Pantry.Packed),
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
                mallStashIds = _mallStash.Select(l => l.Id).ToArray(),
                mallStashCounts = _mallStash.Select(l => l.Count).ToArray(),
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
            Backpack.ClearGifts();
            Backpack.ClearFurniture();
            _furnStock.Clear();
            _homeLayout.Clear();
            _shopLayout.Clear();
            _parcels.Clear();
            _mallStash.Clear();
            SaveHome h = d.home;
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
                _parcels.Add(new Parcel { Kind = (ParcelKind)p.kind, Id = p.id, Count = p.count, DueDay = p.dueDay });
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
            FromSave(h.packed, Pantry.Packed);
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
            _boxGifts.Clear();
            string[] bids = h.boxGiftIds ?? Array.Empty<string>();
            for (int i = 0; i < bids.Length; i++)
                if (h.boxGiftCounts != null && i < h.boxGiftCounts.Length && Gifts.Get(bids[i]) != null && h.boxGiftCounts[i] > 0) _boxGifts[bids[i]] = h.boxGiftCounts[i];
            string[] fids = h.packFurnIds ?? Array.Empty<string>();
            for (int i = 0; i < fids.Length; i++)
                if (h.packFurnCounts != null && i < h.packFurnCounts.Length) Backpack.AddFurniture(fids[i], h.packFurnCounts[i]);
            string[] sids = h.mallStashIds ?? Array.Empty<string>();
            for (int i = 0; i < sids.Length; i++)
                if (h.mallStashCounts != null && i < h.mallStashCounts.Length && h.mallStashCounts[i] > 0 && FurnitureCatalog.Get(sids[i]) != null) AddStash(sids[i], h.mallStashCounts[i]);
            ApplyCargoLimit();
        }
    }
}
