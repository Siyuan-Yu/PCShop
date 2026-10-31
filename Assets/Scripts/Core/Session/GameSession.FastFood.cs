using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    public sealed class FastFoodItem
    {
        public string Id { get; }
        public string Name { get; }
        public Money Price { get; }
        public int Hunger { get; }
        public int Nutrition { get; }
        public int Mood { get; }
        public int Minutes { get; }
        public bool Drink { get; }
        internal FastFoodItem(string id, string name, long yuan, int hunger, int nutrition, int mood, int minutes, bool drink = false)
        { Id = id; Name = name; Price = Money.FromYuan(yuan); Hunger = hunger; Nutrition = nutrition; Mood = mood; Minutes = minutes; Drink = drink; }
        public string Effects => $"饱腹 +{Hunger} · 营养 {GameSession.SignedDiet(Nutrition)} · 心情 {GameSession.SignedDiet(Mood)}";
    }

    public sealed partial class GameSession
    {
        public const string FastFoodBusiness = "快餐";
        public static readonly IReadOnlyList<FastFoodItem> FastFoodMenu = Array.AsReadOnly(new[]
        {
            new FastFoodItem("burger", "鸡腿汉堡", 18, 45, -1, 5, 10),
            new FastFoodItem("chicken", "香酥炸鸡", 16, 35, -2, 6, 10),
            new FastFoodItem("fries", "薯条", 8, 18, -2, 4, 5),
            new FastFoodItem("cola", "可乐", 6, 2, -3, 8, 2, true),
            new FastFoodItem("rice", "鸡肉蔬菜饭", 20, 65, 3, 3, 15),
            new FastFoodItem("combo", "汉堡炸鸡套餐（含可乐）", 32, 90, -4, 10, 20)
        });

        public string FastFoodBlocker(string itemId)
        {
            var item = FastFoodMenu.FirstOrDefault(x => x.Id == itemId);
            if (item == null) return "菜单里没有这种餐品。";
            var p = CurrentPlace;
            if (IsWorking) return "打工期间不能自行点餐。";
            if (p == null || p.Kind != PlaceKind.Shop || p.Business != FastFoodBusiness || IsMyShop(p)
                || IsVacant(p) || VentureAt(p.Id) != null || PropertyAt(p.Id) != null) return "请到营业的快餐店点餐。";
            if (!IsPlaceOpen(p)) return "快餐店已打烊，营业时间：" + OpeningHoursText(p) + "。";
            if (!item.Drink && Player.Hunger >= 99) return "已经吃饱了。";
            if (Ledger.Balance < item.Price) return "现金不足。";
            return null;
        }

        public bool BuyFastFood(string itemId)
        {
            string why = FastFoodBlocker(itemId);
            if (why != null) return Fail(why);
            var item = FastFoodMenu.First(x => x.Id == itemId);
            if (!Ledger.TryDebit(Clock.Today, "餐饮", item.Price, CurrentPlace.Name + " · " + item.Name)) return Fail("现金不足。");
            // Apply the meal before advancing time, so a hungry player is fed while dining.
            Player.Hunger = Math.Min(100, Player.Hunger + item.Hunger);
            ApplyDiet(item.Nutrition, item.Mood);
            AdvanceClock(item.Minutes);
            Say("堂食：" + item.Name + "，花费 " + item.Price + "，" + item.Effects + "。");
            return true;
        }
    }
}
