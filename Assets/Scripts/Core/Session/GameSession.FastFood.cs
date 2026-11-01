using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.World;
using PCShopSell.Core.Home;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        public const string FastFoodBusiness = "快餐";
        public static IReadOnlyList<FastFoodItem> FastFoodMenu => FastFoodCatalog.All;

        public string FastFoodBlocker(string itemId, int count = 1)
        {
            if (count <= 0 || count > 100) return "请选择1到100份。";
            var item = FastFoodMenu.FirstOrDefault(x => x.Id == itemId);
            if (item == null) return "菜单里没有这种餐品。";
            var p = CurrentPlace;
            if (IsWorking) return "打工期间不能自行点餐。";
            if (p == null || p.Kind != PlaceKind.Shop || p.Business != FastFoodBusiness || IsMyShop(p)
                || IsVacant(p) || VentureAt(p.Id) != null || IsPlayerPropertyOccupied(p.Id)) return "请到营业的快餐店点餐。";
            if (!IsPlaceOpen(p)) return "快餐店已打烊，营业时间：" + OpeningHoursText(p) + "。";
            if (!SendByCourier && item.WeightKg * count > FreeKg + 1e-9) return "背包装不下了。";
            if (Ledger.Balance < item.Price * count) return "现金不足。";
            return null;
        }

        public bool BuyFastFood(string itemId, int count = 1)
        {
            string why = FastFoodBlocker(itemId, count);
            if (why != null) return Fail(why);
            var item = FastFoodMenu.First(x => x.Id == itemId);
            if(SendByCourier)return Ship(CourierShop(false),"餐饮",item.Price*count,CurrentPlace.Name+" · "+item.Name,
                new CourierItem {Kind=CourierItemKind.PackedFood,Id="fast:"+item.Id,Count=count,ExpireDay=Clock.Today.TotalDays+item.ShelfDays},"买了");
            if (!Ledger.TryDebit(Clock.Today, "餐饮", item.Price * count, CurrentPlace.Name + " · " + item.Name)) return Fail("现金不足。");
            Backpack.Food.Packed.Add(new FoodBatch { Id = "fast:" + item.Id, Name = item.Name, Hunger = item.Hunger,
                Count = count, ExpireDay = Clock.Today.TotalDays + item.ShelfDays });
            ApplyCargoLimit();
            Say("买了" + item.Name + "×" + count + "，已放入背包，花费 " + item.Price * count + "。可稍后手动吃喝，或留到自动进餐。");
            return true;
        }
    }
}
