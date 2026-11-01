using System.Linq;
using PCShopSell.Core.Home;
using PCShopSell.Game;
using PCShopSell.Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.EditorTools
{
    public static partial class GameSceneBuilder
    {
        public static void FoodExpiryFromBatch() => RunSmokeBatch(() =>
        {
            // Deliberately inspect the rendered hierarchy without taking screenshots.
            ShaderUtil.allowAsyncCompilation = false; UrpSetup.Ensure();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildRig(); _game = new GameObject("Game").AddComponent<GameController>();
            _game.Bootstrap(false, 7); _game.Ui.QuestPopups = false;
            var s = _game.Session; s.EventsEnabled = false; s.Backpack.Food.Packed.Clear();
            s.Clock.Restore(s.Clock.Today, 10 * 60);
            int today = s.Clock.Today.TotalDays;
            s.Backpack.Food.Packed.Add(new FoodBatch { Id = "rice", Name = "白米饭", Hunger = 22, Count = 2, ExpireDay = today });
            s.Backpack.Food.Packed.Add(new FoodBatch { Id = "rice", Name = "白米饭", Hunger = 22, Count = 2, ExpireDay = today + 2 });
            s.Backpack.Food.AddIngredient("veg", 1, today);
            s.Backpack.AddGift(Gifts.Soda, 1); s.Backpack.Food.RiceKg = .25;
            _game.Ui.OpenBackpackPanel(); Frame(0);
            FoodExpiryBadge[] Badges() => _game.Ui.Canvases.SelectMany(c => c.GetComponentsInChildren<FoodExpiryBadge>()).Where(b => b.gameObject.activeInHierarchy).ToArray();
            var rice = Badges().Where(b => b.name == "ExpiryBadge_lunch:rice").ToArray();
            Check(rice.Length == 2 && rice.Select(b => b.ExpireDay).Distinct().Count() == 2, "同名食物不同到期批次显示在不同格子");
            Check(rice.Any(b => b.Label.text == "剩14小时") && rice.Any(b => b.Label.text == "剩3天"), "各批次显示自身剩余时间");
            Check(Badges().Any(b => b.name == "ExpiryBadge_ing:veg"), "食材也显示到期倒计时");
            Check(Badges().Where(b => b.name == "ExpiryBadge_rice" || b.name == "ExpiryBadge_gift:" + Gifts.Soda).All(b => b.Label.text == "无期限"), "无过期规则的食品明确显示无期限");
            var urgent = rice.First(b => b.ExpireDay == today); var rect = (RectTransform)urgent.transform;
            Check(rect.anchorMax.y <= .25f && urgent.Label.alignment == TextAnchor.MiddleRight, "倒计时固定在格子右下角且避开名字与重量");
            s.Clock.Restore(s.Clock.Today, 23 * 60 + 55);
            urgent.Refresh(); Check(urgent.Label.text == "剩5分" && urgent.Label.color.r > urgent.Label.color.g, "时间变化后倒计时刷新并在临期时变红");
            var cell = urgent.GetComponentInParent<Button>(); cell.onClick.Invoke(); Frame(0);
            Check(ActiveButtons().Any(b => ButtonText(b).StartsWith("吃一份白米饭")), "到期角标不妨碍选中与手动食用入口");
            _game.Ui.CloseAllWindows();
        });
    }
}
