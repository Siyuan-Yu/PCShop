using System.Linq;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Core.World;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private int _oldMarketStall;
        private void BuildOldMarket(Window w, Place place)
        {
            Line(w.Content, "营业时间 09:00–22:00", 17, Ui.Dim);
            if (!S.AtOldMarket)
            {
                Ui.Size(Ui.Button(w.Content, "去市场逛逛", () => { CloseAllWindows(); _ctrl.GoToPlace(place); }, 19, Ui.ButtonHot), -1, 46);
                return;
            }
            foreach (var activity in S.CityActivities.Where(a => a.Active && a.VenueId == place.Id))
            {
                string id = activity.Id;
                Ui.Size(Ui.Button(w.Content, "和朋友一起逛", () => OpenCityActivityDetail(id), 18), -1, 42);
            }
            var progress = S.OldMarketProgress;
            bool busy = progress.Pending != null;
            if (busy)
            {
                string name = OldMarketCatalog.Get(progress.GoodsId)?.Name ?? "旧物";
                Line(w.Content, name + " · " + (progress.Pending == "inspect" ? "查看中" : "修理中") + " · 还需" + progress.Remaining + "分钟", 18, Ui.Accent);
                Ui.Size(Ui.Button(w.Content, "继续等待", () => { CloseWindowSilently(w); S.Time.SetPaused(false); }, 18, Ui.ButtonHot), -1, 44);
            }
            var tabs = Ui.Row(w.Content, 42, 6);
            for (int i = 0; i < 5; i++)
            {
                int tab = i;
                Ui.Size(Ui.Button(tabs, i == 4 ? "我买的" : new[] { "钟表", "旧电子", "唱片", "杂货" }[i], () => { _oldMarketStall = tab; w.Rebuild(); }, 18,
                    _oldMarketStall == i ? Ui.ButtonHot : Ui.ButtonBg), -1, 42, 1);
            }
            if (_oldMarketStall == 4)
            {
                if (S.EventIdleItems.Length > 0)
                {
                    var idle = Ui.Row(w.Content, 38, 8);
                    Ui.Size(Ui.Label(idle, "也可处理带来的闲置物品", 17), -1, 38, 1);
                    Ui.Size(Ui.Button(idle, "查看闲置", OpenEventIdleSale, 17), 110, 38);
                }
                var owned = OldMarketCatalog.Gifts.Where(g => S.Backpack.GiftCount(g.Id) > 0).ToArray();
                if (owned.Length == 0) { Para(w.Content, "还没买东西，去摊位看看吧。", 18, Ui.Dim); return; }
                foreach (var item in owned)
                {
                    string key = item.Id;
                    var goods = OldMarketCatalog.All.First(g => g.Id == key || OldMarketCatalog.ItemId(g) == key);
                    Line(w.Content, item.Name + " ×" + S.Backpack.GiftCount(key), 20, Ui.Accent);
                    var actions = Ui.Row(w.Content, 44, 8);
                    if (goods.Faulty && key == OldMarketCatalog.ItemId(goods))
                        Ui.Size(Ui.Button(actions, "请师傅修好 · ¥" + goods.RepairFee + " · 15分钟", () =>
                        {
                            if (!S.AskOldMarketRepair(goods.Id)) return;
                            CloseWindowSilently(w); S.Time.SetPaused(false);
                        }, 17, Ui.ButtonHot, !busy), -1, 44, 1).name = "OldMarketRepair_" + goods.Id;
                    Ui.Size(Ui.Button(actions, "卖掉 · " + S.OldMarketBuybackPrice(key), () => { S.SellOldMarketGoods(key); w.Rebuild(); }, 18, Ui.ButtonBg, !busy), -1, 44, 1);
                }
                return;
            }
            foreach (var goods in OldMarketCatalog.All.Where(g => g.Stall == OldMarketCatalog.Stalls[_oldMarketStall]))
            {
                var g = goods;
                var row = Ui.Row(w.Content, 110, 16);
                row.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>().childForceExpandHeight = false;
                var info = Ui.Rect(row, "OldMarketGoodsInfo"); Ui.VList(info, 4, 0); Ui.Size(info, -1, 104, 1);
                Line(info, g.Name, 21, Ui.Accent);
                Line(info, S.OldMarketPrice(g.Id) + " · " + g.Weight.ToString("0.##") + "kg", 18);
                Para(info, g.Description + (g.Faulty ? " 修理费¥" + g.RepairFee + "。" : ""), 18, Ui.Dim);
                Ui.Size(Ui.Button(row, S.OldMarketRemaining(g.Id) > 0 ? "买下" : "已卖出", () =>
                {
                    if (S.BuyOldMarketGoods(g.Id) && g.Faulty) _oldMarketStall = 4;
                    w.Rebuild();
                }, 18, Ui.ButtonBg, !busy && S.OldMarketRemaining(g.Id) > 0), 96, 36).name = "OldMarketBuy_" + g.Id;
            }
        }
    }
}
