using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;
namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        private static KeyValuePair<string, int>[] SaveRetailCounts(PCShopSell.Core.Shop.ShopSimulation shop) =>
            shop.Stock.GoodsLines().Concat(shop.Queue.Where(c => c.GoodsId != null).Select(c => new KeyValuePair<string, int>(c.GoodsId, 1)))
                .GroupBy(p => p.Key).Select(g => new KeyValuePair<string, int>(g.Key, g.Sum(p => p.Value))).ToArray();
        private static GoodsLot[] SaveRetailLots(PCShopSell.Core.Shop.ShopSimulation shop) =>
            shop.Stock.GoodsLots.Select(l => l.Copy()).Concat(shop.Queue.Where(c => c.GoodsId != null && c.GoodsExpiryDay >= 0)
                .Select(c => new GoodsLot { Id = c.GoodsId, Count = 1, ExpireDay = c.GoodsExpiryDay, Chilled = c.GoodsChilled })).ToArray();
        public bool OtherRetailShopsUnlocked => HasShop && Clock.Today.TotalDays >= 14;
        public string PendingRetailKind { get; private set; } = "computer";
        public string RetailKind => _focus.RetailKind;
        public string RetailTypeBlocker(string kind)
        {
            if (!RetailShopTypes.All.Contains(kind)) return "请选择店铺类型";
            if (kind != "computer" && !OtherRetailShopsUnlocked) return "先开电脑店，第 15 天起可开其他类型的分店";
            return null;
        }
        public bool SignRetailLease(string placeId, bool newShop, string kind)
        {
            string why = RetailTypeBlocker(kind);
            if (why != null) return Fail(why);
            if (!newShop && kind != RetailKind) return Fail("搬店会保留原来的经营类型。");
            if (!SignLease(placeId, newShop)) return false;
            if (PendingLease != null) PendingRetailKind = kind;
            else if (newShop) { _focus.RetailKind = kind; _focus.Sim.RetailKind = kind; }
            return true;
        }
        public bool SetRetailKind(string kind)
        {
            if (!InPlayerShop || _focus == MainShop) return Fail("主店保留电脑店经营，其他类型请在分店选择。");
            string why = RetailTypeBlocker(kind);
            if (why != null) return Fail(why);
            if (!PlayerShop.Stock.IsEmpty || PlayerShop.Queue.Count > 0 || PlayerShop.Orders.Count > 0 || PlayerShop.DisplayPcs.Count > 0 || IncomingShopSpace > 0)
                return Fail("更换类型前请清空库存、展示机、订单和在途货物。");
            if (!BeginRenovation()) return false;
            PlayerShop.ClearCustomers();
            _focus.RetailKind = kind; PlayerShop.RetailKind = kind;
            Say("这家店改为" + RetailShopTypes.Name(kind) + "，请按新类型设置货架，明天恢复营业。");
            return true;
        }
        private int RetailCount(Backpack bag, GiftDef def) => def == null ? 0 : def.IngredientId != null ? bag.Food.Ingredient.Where(b => b.Id == def.IngredientId).Sum(b => b.Count)
            : def.MealId != null ? bag.Food.Packed.Where(b => b.Id == def.Id).Sum(b => b.Count)
            : def.Id == "produce:rice" ? (int)Math.Floor(bag.Food.RiceKg / 0.5 + 1e-9) : bag.GiftCount(def.Id);
        private void AddRetailToBag(Backpack bag, GiftDef def, int count, int expiry, bool chilled = false)
        {
            if (def.Id == "produce:rice") { bag.Food.RiceKg += count * 0.5; return; }
            if (!RetailProducts.IsFood(def)) { bag.AddGift(def.Id, count); return; }
            var meal = FastFoodCatalog.Packed(def.Id);
            var batch = new FoodBatch { Id = def.IngredientId ?? def.Id, Name = def.Name, Count = count,
                ExpireDay = expiry >= 0 ? expiry : Clock.Today.TotalDays + def.ShelfDays,
                Hunger = meal?.Hunger ?? 0, Chilled = chilled, ChilledDays = chilled ? 7 : 0 };
            (def.IngredientId != null ? bag.Food.Ingredient : bag.Food.Packed).Add(batch);
        }
        private FoodBatch TakeRetailFromBag(Backpack bag, GiftDef def)
        {
            if (def.IngredientId != null) return Pantry.TakeBowl(bag.Food.Ingredient, def.IngredientId);
            if (def.MealId != null) return Pantry.TakeBowl(bag.Food.Packed, def.Id);
            if (def.Id == "produce:rice") { if (bag.Food.RiceKg < 0.5) return null; bag.Food.RiceKg -= 0.5; }
            else if (!bag.TakeGift(def.Id)) return null;
            return new FoodBatch { Id = def.Id, Count = 1, ExpireDay = -1 };
        }
        public bool HasRetailWholesale(Place source) => source != null && source.Kind == PlaceKind.Shop && !IsMyShop(source)
            && !IsVacant(source) && VentureAt(source.Id) == null && Gifts.All.Any(g => g.SoldAt == source.Business);
        public bool BuyRetailWholesale(string sourceId, string id, int count, bool toBag, string shopId)
        {
            if (IsGameOver || IsWorking || !City.HasPlace(sourceId)) return false;
            var source = City.GetPlace(sourceId); var def = Gifts.Get(id);
            if (CurrentPlace != source || !HasRetailWholesale(source) || !IsPlaceOpen(source) || def == null || def.SoldAt != source.Business || count < 1 || count > 100)
                return Fail("请到对应货源营业时采购，单项数量为 1–100。");
            Money cost = def.WholesalePrice * count;
            int expiry = def.ShelfDays > 0 ? Clock.Today.TotalDays + def.ShelfDays : -1;
            if (toBag)
            {
                if (!CanCarry(def.WeightKg * count)) return false;
                if (!Ledger.TryDebit(Clock.Today, "进货", cost, source.Name + "批发 " + def.Name + "×" + count)) return Fail("现金不足。");
                AddRetailToBag(Backpack, def, count, expiry); ApplyCargoLimit();
                Say(def.Name + "×" + count + "已放入背包，可带到自己的店里上架。"); return true;
            }
            var dest = ShopAt(shopId);
            if (dest == null) return Fail("请选择收货店铺。");
            double free = 0;
            WithShop(dest, () => free = Math.Max(0, PlayerShop.Stock.FreeSpace - IncomingShopSpace));
            if (Stock.GoodsSpace(id, count) > free + 1e-9) return Fail("收货店的仓储空间不足，请减少数量。");
            return Ship(dest, "进货", cost, source.Name + "批发 " + def.Name + "×" + count,
                new CourierItem { Kind = CourierItemKind.Goods, Id = id, Count = count, ExpireDay = expiry }, "买了");
        }
        public string GoodsExpiryText(string id)
        {
            int expiry = PlayerShop.Stock.GoodsExpiry(id);
            if (expiry < 0) return "无期限";
            long minutes = Math.Max(0, FoodBatch.ExpiryMinute(expiry) - Clock.Now.TotalMinutes);
            return new GameDate(expiry) + $"（还剩 {minutes / 1440} 天 {minutes % 1440 / 60} 小时）";
        }
        private void SpoilRetailGoods(int today)
        {
            foreach (var shop in _shops)
            {
                var lost = shop.Sim.Stock.SpoilGoods(today);
                if (lost.Count > 0) Say(ShopTitle(shop) + "清理过期商品：" + string.Join("、", lost));
            }
        }
    }
}
