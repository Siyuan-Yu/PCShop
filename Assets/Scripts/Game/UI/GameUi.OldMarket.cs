using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Core.World;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private Window _oldMarketWindow;

        private void OpenOldMarket(Place place, string activityId, bool showReception = false)
        {
            if (S.CurrentPlace == place)
            {
                S.PrepareVenueCompanions(place, activityId);
                var booking = S.PendingVenueActivity(place, activityId);
                if (GameSession.IsOldMarketVisit(booking))
                {
                    if (booking.ServiceConfirmed)
                    {
                        if (S.VenueAppointmentStartBlocker(booking) == null) S.StartVenueAppointment(booking.Id);
                    }
                    else if (booking.RendezvousReady && S.VenueServiceBlocker(booking.DefinitionId, 0, booking.Id) == null)
                        S.SelectVenueService(booking.DefinitionId, 0, booking.Id);
                    booking = S.PendingVenueActivity(place, activityId);
                }
                if (!showReception && !GameSession.IsOldMarketVisit(booking)
                    && (S.PendingMarketFind != null || S.MarketHuntRemaining > 0 && booking == null))
                {
                    if (_oldMarketWindow != null && !_oldMarketWindow.Closed) _oldMarketWindow.Close();
                    OpenMarketHunt();
                    return;
                }
            }

            int tier = S.MarketHuntTier;
            int attempts = OldMarketHuntCatalog.PassSizes[0];
            void Build(Window w) => BuildOldMarket(w, place, activityId, tier, attempts,
                index => { tier = index; w.Rebuild(); }, count => { attempts = count; w.Rebuild(); });
            if (_oldMarketWindow == null || _oldMarketWindow.Closed)
            {
                _oldMarketWindow = OpenWindow(place.Name, 1000, 620, Build, fitContent: true);
                return;
            }
            _windows.Remove(_oldMarketWindow);
            _windows.Add(_oldMarketWindow);
            _oldMarketWindow.Root.SetAsLastSibling();
            _oldMarketWindow.Builder = Build;
            _oldMarketWindow.Rebuild();
        }

        private void BuildOldMarket(Window w, Place place, string activityId, int tier, int attempts,
            System.Action<int> selectTier, System.Action<int> selectAttempts)
        {
            if (S.CurrentPlace != place)
            {
                Line(w.Content, CityPlaceHours(place), 17, Ui.Dim);
                Ui.Size(Ui.Button(w.Content, "前往青湾旧物市场", () => { CloseAllWindows(); _ctrl.GoToPlace(place); },
                    19, Ui.ButtonHot), -1, 46);
                return;
            }

            var booking = S.PendingVenueActivity(place, activityId);
            var outing = S.RunningCityActivity;
            bool marketVisit = GameSession.IsOldMarketVisit(outing);
            var definition = marketVisit ? S.CityActivityService(outing) : null;
            var topics = marketVisit ? ActivityTopicIndices(outing, definition) : System.Array.Empty<int>();
            bool showReserved = booking?.ServiceConfirmed == true && !GameSession.IsCommunityMarketOuting(booking);
            var goods = BuildRosterFrame(w, place, showReserved ? 470 : topics.Length > 0 ? 370 : 310);
            Line(goods, CityPlaceHours(place), 17, Ui.Dim);
            if (marketVisit)
            {
                var row = Ui.Row(goods, 40, 8);
                string status = GameSession.IsCommunityMarketOuting(outing)
                    ? GameSession.CommunityMarketGiftDelivered(outing) ? "周维成已收下旧物，结束闲逛后会告诉你线索。"
                        : "和周维成一起淘一件旧物，淘到后可以直接送给他。"
                    : "正在和同行者逛旧物市场。";
                Ui.Size(Ui.Label(row, status, 17, color: Ui.Accent), 0, 40, 1);
                if (S.CanFinishManualCityActivity(outing.Id))
                    Ui.Size(Ui.Button(row, "结束闲逛", () =>
                    {
                        if (S.FinishManualCityActivity(outing.Id)) w.Close();
                    }, 16), 110, 37);
                else
                    Ui.Size(Ui.Button(row, "提前离开", () => ConfirmEndCityActivity(outing, false), 16), 110, 37);
                if (topics.Length > 0)
                {
                    var conversations = Ui.Row(goods, 42, 8);
                    foreach (int topic in topics)
                        Ui.Size(Ui.Button(conversations, definition.Choices[topic], () =>
                        {
                            if (SelectCityActivityOption(outing, definition, topic, "继续逛市场")) w.RequestRebuild();
                        }, 16, Ui.ButtonBg, !outing.Choices.Contains(topic.ToString())), 0, 40, 1);
                }
            }
            else if (GameSession.IsCommunityMarketOuting(booking))
                Para(goods, S.VenueAppointmentStartBlocker(booking) ?? "等待周维成到场。", 17, Ui.Dim);
            else if (showReserved && S.CityActivityService(booking) is CityActivityDefinition reserved)
                BuildConfirmedVenueAppointment(w, booking, reserved, here: true, parent: goods);
            else if (booking?.DefinitionId == "oldmarket_visit")
            {
                string why = S.VenueServiceBlocker(booking.DefinitionId, 0, booking.Id);
                Ui.Size(Ui.Button(goods, "与同行者会合", () =>
                {
                    S.SelectVenueService(booking.DefinitionId, 0, booking.Id);
                    w.Rebuild();
                }, 18, Ui.ButtonBg, why == null).Tip(why), -1, 40);
            }
            // Wait for the booked group before buying searches for the outing.
            if (!marketVisit && GameSession.IsOldMarketVisit(booking)) return;

            bool hasFind = S.PendingMarketFind != null;
            if (hasFind || S.MarketHuntRemaining > 0)
            {
                string caption = hasFind ? "领取上次淘到的旧物" : $"继续淘旧物 · 剩 {S.MarketHuntRemaining} 次";
                Ui.Size(Ui.Button(goods, caption, () => { w.Close(); OpenMarketHunt(); }, 19, Ui.ButtonHot), -1, 45);
            }
            else
            {
                var tiers = Ui.Row(goods, 49, 7);
                for (int i = 0; i < OldMarketHuntCatalog.Tiers.Length; i++)
                {
                    int index = i;
                    Ui.Size(Ui.Button(tiers, $"{OldMarketHuntCatalog.Tiers[i]} · ¥{OldMarketHuntCatalog.Prices[i]:N0}／次",
                        () => selectTier(index), 18, Ui.SelectedButton(tier == i)), 0, 44, 1);
                }
                var sizes = Ui.Row(goods, 45, 7);
                foreach (int count in OldMarketHuntCatalog.PassSizes)
                {
                    int minutes = count * OldMarketHuntCatalog.MinutesPerSearch;
                    string length = minutes < 60 ? $"{minutes} 分钟" : $"{minutes / 60} 小时";
                    Ui.Size(Ui.Button(sizes, $"{count} 次 · {length}", () => selectAttempts(count),
                        17, Ui.SelectedButton(count == attempts)), 0, 40, 1);
                }
                string blocker = S.MarketHuntPassBlocker(tier, attempts);
                if (blocker != null) Para(goods, blocker, 17, Ui.Warn);
                Money price = Money.FromYuan(OldMarketHuntCatalog.Prices[tier] * attempts);
                Ui.Size(Ui.Button(goods, $"支付 {price} · 开始淘货", () =>
                {
                    if (!S.BuyMarketHuntPass(tier, attempts)) { w.Rebuild(); return; }
                    w.Close();
                    OpenMarketHunt();
                }, 20, Ui.ButtonHot, blocker == null), -1, 48).name = "BuyMarketHuntPass";
            }

            bool hasService = S.OldMarketProgress.Pending != null || S.EventIdleItems.Length > 0
                || OldMarketCatalog.Gifts.Any(item => S.Backpack.GiftCount(item.Id) > 0);
            if (!hasService && marketVisit) return;
            var footer = Ui.Row(goods, 40, 8);
            if (hasService)
                Ui.Size(Ui.Button(footer, "旧物修理与回收", OpenOldMarketServices, 17), 0, 37, 1).name = "OldMarketServices";
            if (outing?.VenueId == place.Id && !marketVisit)
                Ui.Size(Ui.Button(footer, "同行活动", () => { w.Close(); OpenCityActivityDetail(outing.Id); }, 17), 0, 37, 1);
            else if (outing?.VenueId != place.Id)
                Ui.Size(Ui.Button(footer, "返回街上", () => { w.Close(); _ctrl.LeaveCurrentPlace(); }, 17), 0, 37, 1);
        }

        private void OpenOldMarketServices()
        {
            OpenWindow("旧物修理与回收", 740, 620, w =>
            {
                var progress = S.OldMarketProgress;
                bool busy = progress.Pending != null;
                bool manualVisit = S.ManualActivityClockPaused && S.RunningCityActivity?.VenueId == GameSession.OldMarketVenueId;
                if (manualVisit)
                {
                    w.LiveConversation = true;
                    w.LiveKey = () => S.OldMarketProgress.Pending ?? "";
                    w.LiveStamp = w.LiveKey();
                }
                if (busy)
                {
                    string name = OldMarketCatalog.Get(progress.GoodsId)?.Name ?? "旧物";
                    Line(w.Content, $"{name} · {(progress.Pending == "inspect" ? "查看中" : "修理中")}", 18, Ui.Accent);
                    if (!manualVisit)
                        Ui.Size(Ui.Button(w.Content, $"等待完成 · 还需 {progress.Remaining} 分钟", () =>
                        {
                            CloseWindowSilently(w);
                            if (_oldMarketWindow != null && !_oldMarketWindow.Closed) CloseWindowSilently(_oldMarketWindow);
                            S.Time.SetPaused(false);
                        }, 18, Ui.ButtonHot), -1, 43);
                }
                if (S.EventIdleItems.Length > 0)
                    Ui.Size(Ui.Button(w.Content, "回收随身闲置", OpenEventIdleSale, 17), -1, 40);
                foreach (var item in OldMarketCatalog.Gifts.Where(g => S.Backpack.GiftCount(g.Id) > 0))
                {
                    string key = item.Id;
                    var goods = OldMarketCatalog.All.First(g => g.Id == key || OldMarketCatalog.ItemId(g) == key);
                    ItemLabel(w.Content, key, item.Name + " ×" + S.Backpack.GiftCount(key), 20, color: Ui.Accent);
                    var actions = Ui.Row(w.Content, 44, 8);
                    if (goods.Faulty && key == OldMarketCatalog.ItemId(goods))
                        Ui.Size(Ui.Button(actions, $"请师傅修好 · ¥{goods.RepairFee} · 15 分钟", () =>
                        {
                            if (!S.AskOldMarketRepair(goods.Id)) return;
                            if (manualVisit) w.RequestRebuild();
                            else
                            {
                                CloseWindowSilently(w);
                                if (_oldMarketWindow != null && !_oldMarketWindow.Closed) CloseWindowSilently(_oldMarketWindow);
                                S.Time.SetPaused(false);
                            }
                        }, 17, Ui.ButtonHot, !busy), -1, 44, 1).name = "OldMarketRepair_" + goods.Id;
                    Ui.Size(Ui.Button(actions, "卖掉 · " + S.OldMarketBuybackPrice(key),
                        () => { S.SellOldMarketGoods(key); w.Rebuild(); }, 18, Ui.ButtonBg, !busy), -1, 44, 1);
                }
                Ui.Size(Ui.Button(w.Content, "返回柜台", w.Close, 17), -1, 40);
            }, fitContent: true);
        }
    }
}
