using System;
using System.Linq;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Game.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.EditorTools
{
    public static partial class GameSceneBuilder
    {
        public static void FoodFromBatch() => RunSmokeBatch(SmokeFood);
        private static void SmokeFood()
        {
            SmokeWorkforce();
            var s = _game.Session;
            s.EventsEnabled = false;
            s.Clock.Restore(s.Clock.Today, 10 * 60);
            var mall = s.City.Places.First(p => p.Business == GameSession.MallBusiness);
            Teleport(s, mall.Id);
            _game.Ui.OnEnteredPlace(mall); Frame(0);
            ClickGuideButton("家里用");
            Check(_game.Ui.Canvases.SelectMany(c => c.GetComponentsInChildren<Text>()).Any(t => t.text.Contains("双门冰箱")), "家具城可以买到家店通用的冰箱");
            Shot("vs1_food_01_fridge_for_sale");
            Check(s.CartAddFurniture(FurnitureCatalog.Fridge), "冰箱加入购物车");
            s.CartAllToTruck(); s.SetCourierDest(null);
            Check(s.Checkout(false), "实际购买冰箱并叫货车送到家");
            s.AdvanceClock((int)(s.Couriers.Max(c => c.DueMinute) - s.Clock.Now.TotalMinutes + 1));
            Teleport(s, s.Home.Id); Frame(0);
            Check(PlaceFoodFridge(false), "买来的冰箱送达后能摆在家里");
            Frame(0); Shot("vs1_food_02_home_fridge");
            s.Pantry.Cooked.Add(new FoodBatch { Id = "veg", Name = "青菜焖饭", Hunger = 28, Count = 3, ExpireDay = s.Clock.Today.TotalDays + 1 });
            Check(s.ChillCookedFood(3), "锅里的青菜焖饭直接放进冰箱");
            s.Backpack.Food.Packed.Clear();
            s.Backpack.Food.Packed.Add(new FoodBatch { Id = "rice", Name = "白米饭", Hunger = 22, Count = 2, ExpireDay = s.Clock.Today.TotalDays + 1 });
            s.Backpack.Food.Packed.Add(new FoodBatch { Id = "pork", Name = "肉末焖饭", Hunger = 36, Count = 2, ExpireDay = s.Clock.Today.TotalDays + 1 });
            _game.HomeInteract(HomeRoomView.Fridge); Frame(0);
            Shot("vs1_food_03_fridge_contents"); Shot("vs1_food_04_fridge_720", 1280, 720);
            ClickGuideButton("取出 1 份");
            Check(s.Backpack.Food.Packed.Any(b => b.Name == "青菜焖饭"), "冰箱取出的熟食保留菜名");
            _game.Ui.CloseAllWindows();
            _game.Ui.OpenBackpackPanel(); Frame(0);
            var vegetable = ActiveButtons().FirstOrDefault(b => ButtonText(b).Contains("青菜焖饭"));
            Check(vegetable != null, "背包按菜名显示青菜焖饭而不是合并饭盒");
            vegetable?.onClick.Invoke(); Frame(0);
            Check(ActiveButtons().Any(b => ButtonText(b) == "吃一份青菜焖饭"), "可选择指定菜名吃饭");
            Shot("vs1_food_05_named_meals"); Shot("vs1_food_06_backpack_720", 1280, 720);
            s.Player.Hunger = 40;
            double nutrition = s.Player.Nutrition;
            ClickGuideButton("吃一份青菜焖饭");
            Check(s.Player.Nutrition == nutrition, "青菜焖饭增加饥饿，营养不加不减");

            var tobacco = s.City.Places.First(p => p.Business == Gifts.TobaccoBusiness);
            Teleport(s, tobacco.Id);
            _game.Ui.OnEnteredPlace(tobacco); Frame(0);
            ClickGuideButton("饮料点心"); Shot("vs1_food_07_cola_for_sale");
            Check(s.CartAddGift(Gifts.Soda, 2), "烟酒茶行可购买可乐"); s.CartFillBag();
            Check(s.Checkout(false), "可乐实际结账进背包");
            _game.Ui.OpenBackpackPanel(); Frame(0);
            var cola = ActiveButtons().FirstOrDefault(b => ButtonText(b).Contains("可乐")); cola?.onClick.Invoke(); Frame(0);
            Shot("vs1_food_08_drink_cola");
            double mood = s.Player.Mood; nutrition = s.Player.Nutrition;
            ClickGuideButton("喝一瓶可乐");
            Check(s.Player.Nutrition == nutrition - 3 && s.Player.Mood == mood + 8, "可乐营养减3、心情加8");

            Teleport(s, s.MainShop.Place.Id);
            s.MainShop.Temp.AddFurniture(FurnitureCatalog.Fridge, 1);
            Check(s.BeginRenovation() && PlaceFoodFridge(true), "同一种冰箱也能摆在店里"); Frame(0);
            Check(s.MoveFridgeFood("lunch:pork", 1, true), "店里的冰箱能存肉末焖饭");
            Check(s.HomeFridge.Lunchboxes == 2 && s.CurrentFridge.Lunchboxes == 1, "家和店的冷藏食物相互独立");
            _game.Ui.OpenFridgeWindow(); Frame(0); Shot("vs1_food_09_shop_fridge");
            _game.Ui.CloseAllWindows();
            Check(!_game.Session.Time.IsHeld, "关闭食物窗口后释放暂停");
        }

        private static bool PlaceFoodFridge(bool shop)
        {
            var s = _game.Session;
            var def = FurnitureCatalog.Get(FurnitureCatalog.Fridge);
            var room = shop ? s.ShopRoom : RoomSpec.Home;
            for (double z = 1; z < room.Depth - 0.5; z += 0.5)
                for (double x = 1; x < room.Width - 0.5; x += 0.5)
                {
                    var at = new PlacedFurniture { Id = def.Id, X = x, Z = z };
                    if (s.PlacementError(shop, def, at, -1) == null && s.PlaceFromStock(shop, def.Id, at)) return true;
                }
            return false;
        }
    }
}
