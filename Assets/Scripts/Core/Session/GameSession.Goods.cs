using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Home;
using PCShopSell.Core.Inventory;

namespace PCShopSell.Core.Session
{
    /// <summary>
    /// 货架：所有货架都一样，每个摆一种货（配件 6 类，或烟、酒、饮料、点心、水果），最多放 60 公斤。
    /// 店里的货都放在后仓（<c>Stock</c>，仓位有限），货架空出来就自动从后仓补；顾客只买货架上摆着的，
    /// 没有对应货架的货，后仓里再多也卖不出去。
    /// 另外：除了电脑配件，礼品表里的东西（烟酒饮料点心水果）也能放进后仓、摆上货架卖，
    /// 来店里的人多半是买配件的，杂货卖得慢，摆什么、卖多少钱自己定。
    /// </summary>
    public sealed partial class GameSession
    {
        /// <summary>店里摆好的货架数。</summary>
        public int ShelvesPlaced => PlacedFixtures(FixtureKind.Shelf);

        /// <summary>货架在店面布局里的下标（按摆放顺序）。</summary>
        public IReadOnlyList<int> ShelfIndexes()
        {
            var l = new List<int>();
            for (int i = 0; i < _shopLayout.Count; i++)
                if (FurnitureCatalog.Get(_shopLayout[i].Id)?.Fixture == FixtureKind.Shelf) l.Add(i);
            return l;
        }

        /// <summary>某个货架（布局下标）现在设成摆哪类货；没设置返回 null。</summary>
        public ShelfKind ShelfKindAt(int layoutIndex) =>
            layoutIndex >= 0 && layoutIndex < _shopLayout.Count ? ShelfKinds.Get(_shopLayout[layoutIndex].Item) : null;

        /// <summary>摆着这类货的货架有几个。</summary>
        public int ShelvesOfKind(string kindId) => _shopLayout.Count(p => FurnitureCatalog.Get(p.Id)?.Fixture == FixtureKind.Shelf && p.Item == kindId);

        /// <summary>设置货架摆哪类货（null = 清空设置，这个货架什么都不摆）。</summary>
        public bool SetShelfKind(int layoutIndex, string kindId)
        {
            if (!InPlayerShop) return Fail("要在自己的店里才能设置货架。");
            if (layoutIndex < 0 || layoutIndex >= _shopLayout.Count || FurnitureCatalog.Get(_shopLayout[layoutIndex].Id)?.Fixture != FixtureKind.Shelf) return false;
            if (kindId != null && ShelfKinds.Get(kindId) == null) return false;
            if (_shopLayout[layoutIndex].Item == kindId) return true;
            _shopLayout[layoutIndex].Item = kindId;
            ApplyShelfCapacity();
            Say(kindId == null ? "这个货架清空了，什么都不摆。" : $"这个货架改摆「{ShelfKinds.NameOf(kindId)}」。");
            return true;
        }

        /// <summary>摆出来的货架带来的后仓仓位（加固、重型货架才有）。</summary>
        internal double ShelfStockBonus() => _shopLayout.Sum(p => FurnitureCatalog.Get(p.Id)?.StockBonus ?? 0);

        /// <summary>把每类货的货架总载重告诉店（顾客只买货架上的）。</summary>
        internal void ApplyShelfCapacity()
        {
            if (PlayerShop == null) return;
            var kg = new Dictionary<string, double>();
            foreach (PlacedFurniture p in _shopLayout)
            {
                FurnitureDef d = p.Item != null ? FurnitureCatalog.Get(p.Id) : null;
                if (d != null && d.Fixture == FixtureKind.Shelf)
                    kg[p.Item] = (kg.TryGetValue(p.Item, out double v) ? v : 0) + d.ShelfKg;
            }
            PlayerShop.SetShelfCapacity(kg);
        }

        /// <summary>现在在店里、能拿来放进后仓的某种杂货有几件（储物柜里的 + 背包里的）。</summary>
        public int GoodsAvailableToList(string giftId) =>
            InPlayerShop ? ShopLocker.GiftCount(giftId) + Backpack.GiftCount(giftId) : 0;

        /// <summary>店里能放进后仓的杂货一共有几件。</summary>
        public int GoodsListableTotal => InPlayerShop ? Gifts.All.Sum(g => GoodsAvailableToList(g.Id)) : 0;

        /// <summary>
        /// 把储物柜 / 背包里的礼品放进后仓（先用储物柜里的），货架上有对应的货架就会自动摆上去。
        /// 需要人在自己店里、仓位还放得下。
        /// </summary>
        public bool ListGoods(string giftId, int count = 1)
        {
            GiftDef g = Gifts.Get(giftId);
            if (g == null || count <= 0) return false;
            if (!InPlayerShop) return Fail("要在自己的店里才能上架。");
            int have = GoodsAvailableToList(giftId);
            if (have < count) return Fail($"手边只有 {have} 件「{g.Name}」。");
            if (!PlayerShop.Stock.CanAddGoods(giftId, count)) return Fail("店里放不下了（仓位不够）。");
            int fromLocker = Math.Min(count, ShopLocker.GiftCount(giftId));
            for (int i = 0; i < fromLocker; i++) ShopLocker.TakeGift(giftId);
            for (int i = 0; i < count - fromLocker; i++) Backpack.TakeGift(giftId);
            PlayerShop.Stock.TryAddGoods(giftId, count);
            ApplyCargoLimit();
            ShelfKind k = ShelfKinds.ForGift(giftId);
            string tail = k != null && ShelvesOfKind(k.Id) == 0 ? $"（还没有摆「{k.Name}」的货架，顾客买不到）" : "";
            Say($"把「{g.Name}」{(count > 1 ? "×" + count : "")}放进了店里。{tail}");
            return true;
        }

        /// <summary>把店里的杂货收回储物柜。</summary>
        public bool UnlistGoods(string giftId, int count = 1)
        {
            GiftDef g = Gifts.Get(giftId);
            if (g == null || count <= 0) return false;
            if (!InPlayerShop) return Fail("要在自己的店里才能下架。");
            if (PlayerShop.Stock.GoodsCount(giftId) < count) return Fail("店里没有这么多。");
            if (ShopLocker.WeightKg + g.WeightKg * count > ShopLockerLimitKg + 1e-9)
                return Fail($"店里纸箱放不下了（{ShopBoxes} 个纸箱，最多 {ShopLockerLimitKg:0} 公斤）。");
            PlayerShop.Stock.TryRemoveGoods(giftId, count);
            ShopLocker.AddGift(giftId, count);
            Say($"把「{g.Name}」{(count > 1 ? "×" + count : "")}从店里收回了储物柜。");
            return true;
        }

        /// <summary>给一种杂货定价（加价倍率，null = 跟随全局）。</summary>
        public void SetGoodsMarkup(string giftId, double? markup)
        {
            if (Gifts.Get(giftId) == null) return;
            PlayerShop.SetGoodsMarkup(giftId, markup);
        }
    }
}
