using System.Linq;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Core.World;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private int _oldMarketStall;
        private Window _oldMarketWindow;

        private void OpenOldMarket(Place place, string activityId)
        {
            if (S.CurrentPlace == place) S.PrepareVenueCompanions(place, activityId);
            if (_oldMarketWindow == null || _oldMarketWindow.Closed)
            {
                _oldMarketWindow = OpenWindow(place.Name, 900, 700, w => BuildOldMarket(w, place, activityId));
                return;
            }
            _windows.Remove(_oldMarketWindow);
            _windows.Add(_oldMarketWindow);
            _oldMarketWindow.Root.SetAsLastSibling();
            _oldMarketWindow.Builder = w => BuildOldMarket(w, place, activityId);
            _oldMarketWindow.Rebuild();
        }

        private void BuildOldMarket(Window w, Place place, string activityId = null)
        {
            Line(w.Content, "营业时间 09:00–22:00", 17, Ui.Dim);
            if (!S.AtOldMarket)
            {
                Ui.Size(Ui.Button(w.Content, "去市场逛逛", () => { CloseAllWindows(); _ctrl.GoToPlace(place); }, 19, Ui.ButtonHot), -1, 46);
                return;
            }
            var booking = S.PendingVenueActivity(place, activityId);
            if (GameSession.IsCommunityMarketOuting(booking) && S.VenueAppointmentStartBlocker(booking) == null)
            {
                S.StartVenueAppointment(booking.Id);
                booking = S.PendingVenueActivity(place, activityId);
            }
            var shopping = S.RunningCityActivity;
            if (shopping?.VenueId != place.Id) shopping = null;
            bool shoppingWithZhou = GameSession.IsCommunityMarketOuting(shopping);
            if (shoppingWithZhou)
            {
                var header = Ui.Row(w.Content, 44, 10);
                Ui.Size(Ui.Label(header, "周维成和你一起逛市场。选一件旧物买下送给他。", 19, color: Ui.Accent), 0, 44, 1);
                Ui.Size(Ui.Button(header, "提前离开", () => ConfirmEndCityActivity(shopping, false), 17), 116, 42);
            }
            else if (GameSession.IsCommunityMarketOuting(booking))
                Para(w.Content, S.VenueAppointmentStartBlocker(booking), 17, Ui.Dim);
            else if (booking?.ServiceConfirmed == true && S.CityActivityService(booking) is CityActivityDefinition reserved)
                BuildConfirmedVenueAppointment(w, booking, reserved, here: true);
            else if (shopping == null && booking?.DefinitionId == "oldmarket_visit")
            {
                string why = S.VenueServiceBlocker(booking.DefinitionId, 0, booking.Id);
                Ui.Size(Ui.Button(w.Content, "开始逛旧物市场", () =>
                {
                    if (S.SelectVenueService(booking.DefinitionId, 0, booking.Id)) w.Close();
                    else w.Rebuild();
                }, 18, Ui.ButtonHot, why == null), -1, 42).Tip(why);
            }
            else if ((shopping ?? booking) is CityActivity outing)
            {
                Ui.Size(Ui.Button(w.Content, "查看同行活动", () => OpenCityActivityDetail(outing.Id), 18), -1, 42);
            }
            Ui.Size(Ui.Button(w.Content, S.MarketHuntRemaining > 0
                ? $"继续淘旧物 · 剩 {S.MarketHuntRemaining} 次" : "去摊位淘旧物",
                () => { w.Close(); OpenMarketHunt(); }, 20, Ui.ButtonHot), -1, 47).name = "OpenMarketHunt";
            var progress = S.OldMarketProgress;
            bool busy = progress.Pending != null;
            bool manualVisit = S.ManualActivityClockPaused && S.RunningCityActivity?.VenueId == place.Id;
            if (manualVisit)
            {
                w.LiveConversation = true;
                w.LiveKey = () => S.OldMarketProgress.Pending ?? "";
                w.LiveStamp = w.LiveKey();
            }
            if (busy)
            {
                string name = OldMarketCatalog.Get(progress.GoodsId)?.Name ?? "旧物";
                string time = manualVisit ? " · 游戏时间暂停" : " · 还需" + progress.Remaining + "分钟";
                Line(w.Content, name + " · " + (progress.Pending == "inspect" ? "查看中" : "修理中") + time, 18, Ui.Accent);
                if (!manualVisit)
                    Ui.Size(Ui.Button(w.Content, "继续等待", () => { CloseWindowSilently(w); S.Time.SetPaused(false); }, 18, Ui.ButtonHot), -1, 44);
            }
            var tabs = Ui.Row(w.Content, 42, 6);
            for (int i = 0; i < 5; i++)
            {
                int tab = i;
                Ui.Size(Ui.Button(tabs, i == 4 ? "我买的" : new[] { "钟表", "旧电子", "唱片", "杂货" }[i], () => { _oldMarketStall = tab; w.Rebuild(); }, 18,
                    Ui.SelectedButton(_oldMarketStall == i)), -1, 42, 1);
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
                    ItemLabel(w.Content, key, item.Name + " ×" + S.Backpack.GiftCount(key), 20, color: Ui.Accent);
                    var actions = Ui.Row(w.Content, 44, 8);
                    if (goods.Faulty && key == OldMarketCatalog.ItemId(goods))
                        Ui.Size(Ui.Button(actions, "请师傅修好 · ¥" + goods.RepairFee + " · 15分钟", () =>
                        {
                            if (!S.AskOldMarketRepair(goods.Id)) return;
                            if (manualVisit) w.RequestRebuild();
                            else { CloseWindowSilently(w); S.Time.SetPaused(false); }
                        }, 17, Ui.ButtonHot, !busy), -1, 44, 1).name = "OldMarketRepair_" + goods.Id;
                    Ui.Size(Ui.Button(actions, "卖掉 · " + S.OldMarketBuybackPrice(key), () => { S.SellOldMarketGoods(key); w.Rebuild(); }, 18, Ui.ButtonBg, !busy), -1, 44, 1);
                }
                return;
            }
            foreach (var goods in OldMarketCatalog.All.Where(g => g.Stall == OldMarketCatalog.Stalls[_oldMarketStall]))
            {
                var g = goods;
                var row = Ui.Row(w.Content, 110, 16);
                ItemPortrait(row, g.Id, 88);
                row.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>().childForceExpandHeight = false;
                var info = Ui.Rect(row, "OldMarketGoodsInfo"); Ui.VList(info, 4, 0); Ui.Size(info, -1, 104, 1);
                Line(info, g.Name, 21, Ui.Accent);
                Line(info, S.OldMarketPrice(g.Id) + " · " + g.Weight.ToString("0.##") + "kg", 18);
                Para(info, g.Description + (g.Faulty ? " 修理费¥" + g.RepairFee + "。" : ""), 18, Ui.Dim);
                Ui.Size(Ui.Button(row, S.OldMarketRemaining(g.Id) > 0 ? (shoppingWithZhou ? "买下并送给周维成" : "买下") : "已卖出", () =>
                {
                    if (shoppingWithZhou)
                    {
                        if (S.BuyCommunityMarketGift(g.Id)) w.Close();
                        return;
                    }
                    if (S.BuyOldMarketGoods(g.Id) && g.Faulty) _oldMarketStall = 4;
                    w.Rebuild();
                }, 18, shoppingWithZhou ? Ui.ButtonHot : Ui.ButtonBg, !busy && S.OldMarketRemaining(g.Id) > 0), shoppingWithZhou ? 186 : 96, 36).name = "OldMarketBuy_" + g.Id;
            }
        }
    }
}
