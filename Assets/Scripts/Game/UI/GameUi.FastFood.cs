using PCShopSell.Core.Session;
using PCShopSell.Core.World;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private void BuildFastFood(Window w, Place place)
        {
            Line(w.Content, "堂食菜单 · " + S.OpeningHoursText(place), 21, Ui.Accent);
            Line(w.Content, $"现金 {S.Ledger.Balance} · 饱腹 {S.Player.Hunger:0}/100 · 营养 {S.Player.Nutrition:0}/100 · 心情 {S.Player.Mood:0}/100", 17);
            Para(w.Content, "点一份就在店里吃喝，扣费并经过用餐时间，不占背包。营养与心情目前只记录，不改变其他能力。", 16, Ui.Dim);
            var list = Ui.Scroll(w.Content, 450);
            foreach (var menuItem in GameSession.FastFoodMenu)
            {
                var item = menuItem;
                string blocker = S.FastFoodBlocker(item.Id);
                var row = Ui.Row(list, 88, 10);
                Ui.Size(Ui.Label(row, $"<b>{item.Name}</b>　{item.Price}\n<size=13><color=#A8B0BA>堂食，不占背包重量</color></size>\n<size=14>{item.Effects} · {item.Minutes}分钟</size>", 18), -1, -1, 1);
                var button = Ui.Button(row, blocker ?? (item.Drink ? "买一杯喝" : "买一份吃"), () =>
                { S.BuyFastFood(item.Id); w.RequestRebuild(); }, 17, blocker == null ? Ui.ButtonHot : Ui.ButtonBg, blocker == null);
                button.name = "BuyFastFood_" + item.Id;
                Ui.Size(button, 160, 50);
            }
            Para(w.Content, "饿得厉害可选鸡肉蔬菜饭或套餐；油炸食品和可乐能改善心情，但会降低营养记录。", 16, Ui.Dim);
        }
    }
}
