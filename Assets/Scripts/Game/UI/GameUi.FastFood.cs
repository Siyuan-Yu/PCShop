using PCShopSell.Core.Session;
using PCShopSell.Core.World;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private void BuildFastFood(Window w, Place place)
        {
            Line(w.Content, "外带菜单 · " + S.OpeningHoursText(place), 21, Ui.Accent);
            Line(w.Content, $"现金 {S.Ledger.Balance} · 饱腹 {S.Player.Hunger:0}/100 · 营养 {S.Player.Nutrition:0}/100 · 心情 {S.Player.Mood:0}/100", 17);
            Para(w.Content, "买后放进背包，不会当场吃掉。饭点或饿了自动进餐，也可在背包主动吃喝；营养和心情暂只记录。", 16, Ui.Dim);
            var list = Ui.Scroll(w.Content, 450);
            foreach (var menuItem in GameSession.FastFoodMenu)
            {
                var item = menuItem;
                var row = Ui.Row(list, 88, 10);
                Ui.Size(Ui.Label(row, ProductLabel($"<b>{item.Name}</b>　{item.Price}", item.WeightKg,
                    item.Effects + " · 吃喝需" + item.Minutes + "分钟", item.Drink ? "每杯" : "每份"), 18), -1, -1, 1);
                int count = OnlineQuantity(w, row, "fastfood_" + item.Id);
                string blocker = S.FastFoodBlocker(item.Id, count);
                var button = Ui.Button(row, blocker ?? $"购买{count}份 · {item.Price * count}\n放进背包", () =>
                { S.BuyFastFood(item.Id, count); w.RequestRebuild(); }, 17, blocker == null ? Ui.ButtonHot : Ui.ButtonBg, blocker == null);
                button.name = "BuyFastFood_" + item.Id;
                Ui.Size(button, 180, 60);
            }
            Para(w.Content, "饿得厉害可选鸡肉蔬菜饭或套餐；油炸食品和可乐能改善心情，但会降低营养记录。", 16, Ui.Dim);
        }
    }
}
