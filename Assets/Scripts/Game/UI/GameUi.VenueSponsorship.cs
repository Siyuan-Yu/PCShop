using System;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.World;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private enum VenueSponsorPage { Services, Activities, Sponsor, Report }

        private sealed class VenueSponsorView
        {
            public VenueSponsorPage Page;
            public Button Events, Sponsor;
            public string ShopId, ProductKey, ReportId;
            public int Fee, Quantity = 1;
            public long PriceCents;
            public VenueSponsorPlan Plan;
            public int ServiceChoice;
            public Action RaceActivityEntry;
        }

        private void BuildVenueSponsorHeader(Window w, Place place, VenueSponsorView view, RectTransform leisureHeader = null)
        {
            if (S.CurrentPlace != place || !S.HasVenueSponsorship(place)) return;
            void Go(VenueSponsorPage page)
            {
                bool guide = place.Id == GameSession.VenueSponsorGuidePlaceId && S.VenueSponsorGuidePaused;
                view.Page = page;
                w.ResetScrollOnRebuild = true;
                w.Rebuild();
                if (!guide) return;
                if (page == VenueSponsorPage.Activities) S.ViewVenueSponsorGuideActivities(place);
                else if (page == VenueSponsorPage.Sponsor) S.FinishVenueSponsorGuide(place);
            }

            if (leisureHeader != null)
            {
                view.Events = Ui.Button(leisureHeader, "近期活动", () => Go(VenueSponsorPage.Activities), 20);
                view.Sponsor = Ui.Button(leisureHeader, "✦  赞助活动", () => Go(VenueSponsorPage.Sponsor), 20, Ui.ButtonHot);
                ((RectTransform)view.Events.transform).Anchor(.62f, .15f, .715f, .85f);
                ((RectTransform)view.Sponsor.transform).Anchor(.725f, .15f, .84f, .85f);
                var title = leisureHeader.GetComponentsInChildren<Text>().FirstOrDefault(t => t.text == place.Name);
                if (title != null) title.rectTransform.Anchor(.17f, 0, .60f, 1);
            }
            else if (view.Events == null && w.TitleText != null)
            {
                var header = w.TitleText.rectTransform.parent;
                view.Events = Ui.Button(header, "近期活动", () => Go(VenueSponsorPage.Activities), 18);
                view.Sponsor = Ui.Button(header, "✦  赞助活动", () => Go(VenueSponsorPage.Sponsor), 18, Ui.ButtonHot);
                ((RectTransform)view.Events.transform).Anchor(1, 0, 1, 1, -350, 8, 246, 8);
                ((RectTransform)view.Sponsor.transform).Anchor(1, 0, 1, 1, -236, 8, 104, 8);
                w.TitleText.rectTransform.offsetMax = new Vector2(-460, w.TitleText.rectTransform.offsetMax.y);
                if (w.TimeStatusText != null) w.TimeStatusText.rectTransform.Anchor(1, 0, 1, 1, -454, 0, 360, 0);
            }
            if (view.Sponsor == null) return;
            if (place.Id == GameSession.VenueSponsorGuidePlaceId)
            {
                _venueSponsorGuideWindow = w;
                _venueSponsorGuideView = view;
            }
            view.Events.name = "VenueRecentActivities";
            view.Sponsor.name = "VenueSponsorActivity";
            Ui.SetButtonFill(view.Events, Ui.SelectedButton(view.Page == VenueSponsorPage.Activities));
            var sponsorInk = view.Sponsor.GetComponentInChildren<Text>();
            if (sponsorInk != null) sponsorInk.fontStyle = FontStyle.Bold;
        }

        private bool BuildVenueSponsorPage(Window w, Place place, VenueSponsorView view, Transform parent)
        {
            if (view.Page == VenueSponsorPage.Services || S.CurrentPlace != place || !S.HasVenueSponsorship(place)) return false;
            switch (view.Page)
            {
                case VenueSponsorPage.Activities:
                    BuildVenueRecentActivities(w, place, view, parent);
                    break;
                case VenueSponsorPage.Report:
                    var report = S.VenueSponsorships.FirstOrDefault(a => a.Id == view.ReportId);
                    if (report != null)
                    {
                        BuildVenueSponsorReport(parent, report);
                        BuildVenueCommunityActions(parent, w, place, view, report);
                    }
                    break;
                default:
                    BuildVenueSponsorForm(w, place, view, parent);
                    break;
            }
            Ui.Size(Ui.Button(parent, "返回场所菜单", () =>
            {
                view.Page = VenueSponsorPage.Services;
                w.ResetScrollOnRebuild = true;
                w.Rebuild();
            }, 17), -1, 38);
            return true;
        }

        private void BuildVenueRecentActivities(Window w, Place place, VenueSponsorView view, Transform parent)
        {
            Line(parent, "近期活动", 24, Ui.Accent);
            long since = S.Clock.Now.TotalMinutes - 30 * 1440;
            var sponsors = S.VenueSponsorships.Where(a => a.VenueId == place.Id && a.Starts >= since)
                .Select(a => (a.Title, a.Starts, a.Status, a.Completed, Open: (Action)(() =>
                {
                    view.Page = VenueSponsorPage.Report;
                    view.ReportId = a.Id;
                    w.Rebuild();
                })));
            var events = S.CityActivities.Where(a => a.VenueId == place.Id && a.ScheduledStart >= since
                    && S.CityActivityService(a)?.EventOnly == true)
                .Select(a => (Title: S.CityActivityService(a).Name, Starts: a.ScheduledStart,
                    Status: GameSession.CityPhaseText(a.State), Completed: !a.Active, Open: (Action)null));
            var all = sponsors.Concat(events).OrderBy(a => a.Completed)
                .ThenBy(a => a.Completed ? -a.Starts : a.Starts).ToArray();
            var upcoming = S.ActiveVenueCommunity(place) == null ? view.Plan ??= S.PlanVenueSponsorship(place, true) : null;
            bool offer = upcoming is { Starts: >= 0 };
            var list = all.Length + (offer ? 1 : 0) > 7 ? Ui.Scroll(parent, 410) : parent;
            if (offer) AddRow(upcoming.Title, upcoming.Starts, "开放报名", () => Register(), "报名参加");
            foreach (var activity in all) AddRow(activity.Title, activity.Starts, activity.Status, activity.Open);
            if (all.Length == 0 && !offer) Line(parent, "近期暂无公开活动。", 18, Ui.Dim);

            void Register()
            {
                var failure = S.RegisterVenueCommunity(place);
                if (failure != null) { Toast(failure); return; }
                view.ReportId = S.ActiveVenueCommunity(place)?.Id;
                view.Page = VenueSponsorPage.Report;
                w.Rebuild();
            }

            void AddRow(string title, long start, string status, Action open = null, string action = "查看详情")
            {
                var row = Ui.Row(list, 52, 10);
                Ui.Size(ProductText(row, $"<b>{title}</b>　{CityTime(start)}", 18), 0, 50, 1);
                Ui.Size(Ui.Label(row, status, 16, TextAnchor.MiddleRight, Ui.Good), 112, 50);
                if (open != null) Ui.Size(Ui.Button(row, action, open, 16), 96, 38);
            }
        }

        private void BuildVenueSponsorForm(Window w, Place venue, VenueSponsorView view, Transform parent)
        {
            var active = S.ActiveVenueCommunity(venue);
            if (active != null && (active.FeeCents > 0 || active.Running))
            {
                BuildVenueSponsorReport(parent, active);
                BuildVenueCommunityActions(parent, w, venue, view, active);
                return;
            }
            if (S.VenueSponsorBlocker(venue) is string blocker)
            {
                Line(parent, "赞助条件", 24, Ui.Accent);
                if (S.VenueSponsorContact(venue) is CityPersonState contact)
                {
                    var row = Ui.Row(parent, 76, 12);
                    var portrait = Ui.Size(Ui.Rect(row, "VenueSponsorContact"), 58, 70);
                    FitPortrait(portrait, contact.PersonId).raycastTarget = false;
                    Ui.Size(ProductText(row, $"{S.CityPerson(contact.PersonId)?.Name} · 熟悉度 "
                        + $"{S.Relation(contact.PersonId)} / {GameSession.VenueSponsorFamiliarity}", 18), 0, 70, 1);
                    Ui.Size(Ui.Button(row, "交谈", () => OpenPersonDialogue(contact.PersonId, onClosed: w.RequestRebuild), 17), 96, 40);
                }
                Para(parent, blocker, 18, Ui.Dim);
                return;
            }
            var plan = view.Plan ??= S.PlanVenueSponsorship(venue, true);
            if (plan is not { Starts: >= 0 })
            {
                Para(parent, "近期找不到足够街坊有空的时段。", 18, Ui.Dim);
                return;
            }
            Line(parent, plan.Title + " · 赞助方案", 24, Ui.Accent);
            Line(parent, $"{CityTime(plan.Starts)} · 赞助用于{plan.Funding} · {plan.Exposure}", 17, Ui.Dim);
            var shops = S.MyShops.ToArray();
            int index = Math.Max(0, Array.FindIndex(shops, shop => shop.Place.Id == view.ShopId));
            var shop = shops[index];
            view.ShopId = shop.Place.Id;
            view.Fee = Math.Clamp(view.Fee, plan.MinimumFee, plan.MaximumFee);
            var storeRow = Ui.Row(parent, 40, 10);
            Ui.Size(Ui.Label(storeRow, "参与门店", 18), 98, 40);
            if (shops.Length == 1) Ui.Size(Ui.Label(storeRow, S.ShopTitle(shop), 18), 0, 40, 1);
            else Ui.Size(Ui.Choice(storeRow, shops.Select(S.ShopTitle), index, next =>
            {
                view.ShopId = shops[next].Place.Id;
                view.ProductKey = null;
                w.RequestRebuild();
            }), 0, 40, 1);

            var columns = Ui.Row(parent, 256, 12);
            var sponsor = VenueSponsorColumn(columns);
            var sale = VenueSponsorColumn(columns);
            Text estimates = null, purchaseIntent = null;
            VenueSponsorProduct product = null;
            void Refresh()
            {
                int guests = plan.EstimatedGuests(view.Fee);
                if (estimates != null)
                    estimates.text = $"预计到场　约 {guests} 人\n柜台知名度　+{GameSession.VenueSponsorAwareness(guests):0.0}\n"
                        + $"柜台口碑　+{GameSession.VenueSponsorReputation(guests):0.0}";
                if (purchaseIntent != null)
                {
                    double rate = S.VenueSponsorPurchaseIntent(shop, product, Money.FromCents(view.PriceCents));
                    purchaseIntent.text = $"预计购买意愿 {rate:P0} · 约售 {Math.Min(view.Quantity, (int)Math.Round(guests * rate))} 件";
                }
            }
            VenueSponsorshipSlider(sponsor, "赞助费", plan.MinimumFee, plan.MaximumFee, view.Fee,
                value => Money.FromYuan(value).ToString(), value => { view.Fee = (int)value; Refresh(); });
            estimates = Para(sponsor, "", 18, Ui.Good);
            Line(sponsor, "具名街坊赴约 · 按实际到场结算", 15, Ui.Dim);
            Line(sponsor, "知名度和口碑计入本店柜台", 15, Ui.Dim);

            var products = S.VenueSponsorProducts(shop).ToArray();
            int chosen = Array.FindIndex(products, item => item.Key == view.ProductKey);
            product = chosen >= 0 ? products[chosen] : null;
            Line(sale, "活动特卖", 18, Ui.Accent);
            if (products.Length == 0) Line(sale, "这家店暂无可参加特卖的现货。", 16, Ui.Dim);
            else Ui.Size(Ui.Choice(sale, new[] { "不参加特卖" }.Concat(products.Select(item => item.Name)), chosen + 1, next =>
            {
                var selection = next == 0 ? null : products[next - 1];
                view.ProductKey = selection?.Key;
                view.Quantity = Math.Min(10, selection?.Count ?? 1);
                view.PriceCents = selection == null ? 0 : Math.Clamp(selection.Price.Scale(.8).Cents, 1, selection.Price.Cents - 1);
                w.RequestRebuild();
            }), -1, 33);
            if (product != null)
            {
                view.Quantity = Math.Clamp(view.Quantity, 1, product.Count);
                view.PriceCents = Math.Clamp(view.PriceCents, 1, product.Price.Cents - 1);
                var pair = Ui.Row(sale, 65, 10);
                var quantity = VenueSponsorColumn(pair, padded: false);
                var price = VenueSponsorColumn(pair, padded: false);
                VenueSponsorshipSlider(quantity, "数量", 1, product.Count, view.Quantity,
                    value => $"{value} 件", value => { view.Quantity = (int)value; Refresh(); });
                VenueSponsorshipSlider(price, "活动价", 1, product.Price.Cents - 1, view.PriceCents,
                    value => Money.FromCents(value).ToString(), value => { view.PriceCents = value; Refresh(); });
                purchaseIntent = Line(sale, "", 17, Ui.Good);
                Line(sale, $"店内售价 {product.Price} · 最多 {product.Count} 件 · 未售退回原店", 15, Ui.Dim);
            }
            Refresh();
            Ui.Size(Ui.Button(parent, "确认赞助", () =>
            {
                var failure = S.BookVenueSponsorship(venue, view.ShopId, view.Fee, product?.Key, view.Quantity, Money.FromCents(view.PriceCents));
                if (failure != null) { Toast(failure); view.Plan = null; }
                w.Rebuild();
            }, 19, Ui.ButtonHot), -1, 44);
        }

        private static RectTransform VenueSponsorColumn(Transform parent, bool padded = true)
        {
            var column = padded ? Ui.FlatPanel(parent, "VenueSponsorSection", Ui.FlatWell).rectTransform : Ui.Rect(parent, "VenueSponsorFields");
            Ui.VList(column, 4, padded ? 9 : 0);
            Ui.Size(column, 0, -1, 1);
            return column;
        }

        private static void VenueSponsorshipSlider(Transform parent, string caption, long minimum, long maximum, long selected,
            Func<long, string> format, Action<long> onChange)
        {
            var row = Ui.Row(parent, 26, 4);
            Ui.Size(Ui.Label(row, caption, 16), 0, 26, 1);
            var readout = Ui.Size(Ui.Label(row, format(selected), 16, TextAnchor.MiddleRight, Ui.Accent), 86, 26);
            var control = Ui.Size(Ui.Rect(parent, caption), -1, 24);
            var slider = control.gameObject.AddComponent<Slider>();
            Ui.FlatPanel(control, "Track", Ui.Edge).rectTransform.Anchor(0, .35f, 1, .65f, 8, 0, 8, 0);
            var fillArea = Ui.Rect(control, "FillArea").Anchor(0, .35f, 1, .65f, 8, 0, 8, 0);
            var fill = Ui.FlatPanel(fillArea, "Fill", Ui.Good);
            fill.rectTransform.Fill();
            var track = Ui.Rect(control, "HandleArea").Anchor(0, 0, 1, 1, 9, 0, 9, 0);
            var handle = Ui.FlatPanel(track, "Handle", Ui.ButtonHot);
            handle.rectTransform.Anchor(0, .08f, 0, .92f);
            handle.rectTransform.sizeDelta = new Vector2(18, 0);
            slider.targetGraphic = handle;
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.minValue = 0;
            slider.maxValue = Mathf.Min(10000, maximum - minimum);
            slider.wholeNumbers = true;
            slider.interactable = minimum != maximum;
            float steps = slider.maxValue;
            slider.SetValueWithoutNotify(steps > 0 ? (float)((selected - minimum) * (double)steps / (maximum - minimum)) : 0);
            slider.onValueChanged.AddListener(value =>
            {
                long amount = steps > 0 ? minimum + (long)Math.Round((maximum - minimum) * value / (double)steps) : minimum;
                readout.text = format(amount);
                onChange(amount);
            });
        }

        private void BuildVenueSponsorReport(Transform parent, VenueSponsorship activity)
        {
            Line(parent, activity.Title + " · " + activity.Status, 24, Ui.Accent);
            var sponsor = activity.FeeCents > 0 ? $" · {activity.ShopName}赞助 {Money.FromCents(activity.FeeCents)}" : "";
            var player = activity.PlayerJoined ? " · 已到场参加" : activity.PlayerRegistered ? " · 已报名" : "";
            Line(parent, $"{CityTime(activity.Starts)}{sponsor}{player}", 17, Ui.Dim);
            if (!activity.Completed)
                Line(parent, activity.Running ? $"实际到场街坊 {activity.Arrived.Length} 人" : "场所负责组织 · 已记入日程", 18, Ui.Good);
            else if (activity.Cancelled) Line(parent, "未能举办" + (activity.FeeCents > 0 ? " · 赞助费已退回" : ""), 18, Ui.Warn);
            else
            {
                Line(parent, $"实际到场 {activity.Arrived.Length} 人 · 具名街坊 {activity.Arrived.Length} 人", 19, Ui.Good);
                if (activity.FeeCents > 0)
                    Line(parent, $"柜台知名度 +{activity.Awareness:0.0}　柜台口碑 +{activity.Reputation:0.0}", 19, Ui.Good);
            }
            if (activity.Arrived.Length > 0)
                Para(parent, string.Join("、", activity.Arrived.Select(id => S.CityPerson(id)?.Name ?? id)), 16, Ui.Dim);
            if (!string.IsNullOrWhiteSpace(activity.PlayerResult))
                Para(parent, "我的现场记录 · " + activity.PlayerResult, 17, Ui.Accent);
            if (activity.VenueId == GameSession.OldMarketVenueId)
                Line(parent, "本场主题 · " + OldMarketHuntCatalog.Stalls[GameSession.VenueMarketTheme(activity.Starts)], 17, Ui.Accent);
            if (activity.Product is GameSession.EventStock product)
            {
                if (activity.SalePriceInvalid) Line(parent, "门店现价已低于或等于活动价，本场特卖未售。", 16, Ui.Warn);
                else if (activity.Completed && !activity.Cancelled)
                    Line(parent, $"活动特卖购买意愿 {activity.PurchaseIntent:P0}", 16, Ui.Good);
                Line(parent, !activity.Completed ? $"{product.Name} · 备货 {product.Count} 件 · {Money.FromCents(activity.SaleCents)}"
                    : $"{product.Name} · 售出 {activity.Sold} 件 · 退回 {activity.Returned} 件"
                        + (activity.Expired > 0 ? $" · 过期 {activity.Expired} 件" : "")
                        + $" · 收入 {Money.FromCents(activity.RevenueCents)}", 17);
            }
        }

        private void BuildVenueCommunityActions(Transform parent, Window w, Place venue, VenueSponsorView view, VenueSponsorship activity)
        {
            if (activity.Completed) return;
            if (!activity.Running)
            {
                if (!activity.PlayerRegistered)
                    Ui.Size(Ui.Button(parent, "报名参加", () =>
                    {
                        var reason = S.RegisterVenueCommunity(venue);
                        if (reason != null) Toast(reason);
                        else w.Rebuild();
                    }, 18, Ui.ButtonHot), -1, 39);
                return;
            }
            var blocker = S.VenueCommunityPlayBlocker(activity);
            switch (S.VenueCommunityType(venue))
            {
                case VenueCommunityKind.Fitness:
                    Line(parent, $"训练进度 {activity.FitnessRounds} / 3", 17, Ui.Accent);
                    if (activity.FitnessRounds >= 3) return;
                    var modes = Ui.Row(parent, 38, 8);
                    string[] names = { "快走", "器械", "拉伸" };
                    for (int i = 0; i < names.Length; i++)
                    {
                        int mode = i;
                        Ui.Size(Ui.Button(modes, names[i] + " · 10 分钟", () =>
                        {
                            if (S.DoVenueFitnessRound(activity, mode)) w.Rebuild();
                        }, 17, Ui.ButtonHot, blocker == null).Tip(blocker), 0, 38, 1);
                    }
                    break;
                case VenueCommunityKind.Racing:
                    var track = RaceTracks.Get(venue.Id);
                    bool formal = track?.Tier >= 2 && S.CurrentRaceMeeting != RaceMeeting.None;
                    Ui.Size(Ui.Button(parent, formal ? "前往赛场接待 · 观赛或参赛" : "加入车友练习", () =>
                    {
                        if (!S.AttendVenueCommunityRace(activity)) return;
                        view.Page = VenueSponsorPage.Services;
                        view.RaceActivityEntry?.Invoke();
                        w.Rebuild();
                    }, 17, Ui.ButtonHot, blocker == null && view.RaceActivityEntry != null).Tip(blocker), -1, 38);
                    break;
                default:
                    var services = S.VenueCommunityServices(venue);
                    if (services.Length == 0) return;
                    int index = Math.Clamp(view.ServiceChoice, 0, services.Length - 1);
                    var service = services[index];
                    var row = Ui.Row(parent, 41, 8);
                    if (services.Length > 1)
                        Ui.Size(Ui.Choice(row, services.Select(s => s.Name), index, selected =>
                        {
                            view.ServiceChoice = selected;
                            w.RequestRebuild();
                        }), 0, 37, 1);
                    else Ui.Size(Ui.Label(row, service.Name, 17), 0, 37, 1);
                    var price = S.VenueServicePayment(service.Definition, service.Choice, 1, false);
                    string fee = activity.FeeCents > 0 ? " · 本场已补贴" : price.IsPositive ? $" · {price}" : "";
                    Ui.Size(Ui.Button(row, "和街坊一起参加" + fee, () =>
                    {
                        if (S.BeginVenueCommunityService(activity, service)) OpenVenueCommunityPlay(w, venue);
                    }, 17, Ui.ButtonHot, blocker == null).Tip(blocker), 244, 39);
                    break;
            }
        }

        private void OpenVenueCommunityPlay(Window parent, Place venue)
        {
            parent.Close();
            if (S.FishingActive) OpenFishing();
            else if (S.CurrentOnsite != null) OpenOnsitePlay();
            else if (S.LiveLeisure != null) OpenLeisurePlay();
            else if (GameSession.IsKaraokeActivity(S.RunningCityActivity)) OpenKaraokeRoom();
            else if (venue.Id == GameSession.OldMarketVenueId)
            {
                if (S.MarketHuntRemaining > 0) OpenMarketHunt();
                else OpenOldMarket(venue, null, showReception: true);
            }
            else if (GameSession.IsBarVisit(S.RunningCityActivity)) OpenBarCounter(venue);
            else if (S.RunningCityActivity is CityActivity activity) TryOpenVenueActivityCg(activity);
        }

        private void OpenVenueSponsorReport(string id)
        {
            var activity = S.VenueSponsorships.FirstOrDefault(a => a.Id == id);
            if (activity == null) return;
            OpenWindow(activity.Title, 860, 550, w =>
            {
                BuildVenueSponsorReport(w.Content, activity);
                if (!activity.Completed && S.City.HasPlace(activity.VenueId))
                    Ui.Size(Ui.Button(w.Content, "前往场所", () =>
                    {
                        var place = S.City.GetPlace(activity.VenueId);
                        CloseAllWindows();
                        if (S.CurrentPlace == place) OnEnteredPlace(place);
                        else _ctrl.GoToPlace(place);
                    }, 18, Ui.ButtonHot), -1, 40);
            });
        }
    }
}
