using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Home;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    public sealed class VenueSponsorPlan
    {
        public string Title, Funding, Exposure;
        public int MinimumFee, MaximumFee, Capacity, Duration;
        public long Starts;
        internal string[] Guests = Array.Empty<string>();
        public int EstimatedGuests(int fee)
        {
            double fraction = Math.Clamp((fee - MinimumFee) / (double)(MaximumFee - MinimumFee), 0, 1);
            int target = 4 + (int)Math.Round((Capacity - 4) * (1 - (1 - fraction) * (1 - fraction)));
            return Math.Min(target, Guests.Length);
        }
    }

    public sealed class VenueSponsorProduct
    {
        public string Key, Name;
        public int Count;
        public Money Price;
        internal GameSession.EventStock Stock;
    }

    [Serializable]
    public sealed class VenueSponsorship
    {
        public string Id, VenueId, Title, ShopId, ShopAccount, ShopName, ContactId;
        public long Starts, Ends, FeeCents, SaleCents, RevenueCents;
        public string[] Guests = Array.Empty<string>(), Arrived = Array.Empty<string>();
        public GameSession.EventStock Product;
        public GoodsLot[] Lots = Array.Empty<GoodsLot>();
        public int DispatchedCount, Sold, Returned, Expired;
        public bool Running, Completed, Cancelled, PlayerRegistered, PlayerJoined;
        public int FitnessRounds;
        public string PlayerResult;
        public bool MarketPassGranted;
        public string[] PlayedTogether = Array.Empty<string>();
        public double Awareness, Reputation, PurchaseIntent;
        public bool SalePriceInvalid;
        public string Status => Cancelled ? "已取消" : Completed ? "已结算" : Running ? "进行中" : "已安排";

        internal VenueSponsorship Copy()
        {
            var copy = (VenueSponsorship)MemberwiseClone();
            copy.Guests = Guests?.ToArray() ?? Array.Empty<string>();
            copy.Arrived = Arrived?.ToArray() ?? Array.Empty<string>();
            copy.PlayedTogether = PlayedTogether?.ToArray() ?? Array.Empty<string>();
            copy.Product = string.IsNullOrEmpty(Product?.Id) ? null : Product.Copy();
            copy.Lots = Lots?.Where(lot => lot != null).Select(lot => lot.Copy()).ToArray() ?? Array.Empty<GoodsLot>();
            return copy;
        }
    }

    public sealed partial class SaveData { public VenueSponsorship[] venueSponsorships; }

    public sealed partial class GameSession
    {
        public const int VenueSponsorFamiliarity = 30, VenueSponsorStockLimit = 30;
        private const string VenueSponsorPrefix = "venue-sponsor:";
        private readonly List<VenueSponsorship> _venueSponsorships = new();
        public IReadOnlyList<VenueSponsorship> VenueSponsorships => _venueSponsorships;
        public bool HasVenueSponsorship(Place venue) => VenueCommunityAt(venue) != null && !IsOwnedVenue(venue);
        public bool VenueSponsorGuest(CityPersonState person) => person?.ActivityId?.StartsWith(VenueSponsorPrefix, StringComparison.Ordinal) == true;

        public CityPersonState VenueSponsorContact(Place venue)
        {
            if (!HasVenueSponsorship(venue)) return null;
            return PeopleInPlace(venue.Id).Where(p => IsOnDutyVenueNpc(venue, p))
                .OrderByDescending(p => Relation(p.PersonId)).ThenBy(p => p.PersonId, StringComparer.Ordinal).FirstOrDefault();
        }

        public string VenueSponsorBlocker(Place venue)
        {
            if (!HasVenueSponsorship(venue) || CurrentPlace != venue) return "请到场所现场办理。";
            if (!MyShops.Any()) return "需要先拥有一家自营门店。";
            if (RaceTracks.Get(venue.Id) != null && RacingAccessBlocker is string racingBlocker) return racingBlocker;
            var active = ActiveVenueCommunity(venue);
            if (active?.FeeCents > 0) return "这家场所还有一场赞助活动未结束。";
            if (active != null && Clock.Now.TotalMinutes >= active.Starts - 90) return "本场已开始集合，赞助截止。";
            var contact = VenueSponsorContact(venue);
            if (contact == null) return "现在没有工作人员可以办理。";
            return Relation(contact.PersonId) < VenueSponsorFamiliarity ? "与当班联系人熟悉度达到 30 后可以办理。" : null;
        }

        public VenueSponsorPlan PlanVenueSponsorship(Place venue, bool findGuests)
        {
            if (!HasVenueSponsorship(venue)) return null;
            var profile = VenueCommunityAt(venue);
            var plan = new VenueSponsorPlan
            {
                Title = profile.Title, Funding = profile.Funding, Exposure = profile.Exposure,
                MinimumFee = profile.MinimumFee, MaximumFee = profile.MaximumFee, Capacity = profile.Capacity, Duration = profile.Duration
            };
            InitializeCityPeople();
            var active = ActiveVenueCommunity(venue);
            if (active != null)
            {
                plan.Starts = active.Starts;
                plan.Duration = (int)(active.Ends - active.Starts);
                if (findGuests) plan.Guests = VenueSponsorGuests(venue, active.Starts, plan.Duration, plan.Capacity + 3, active.Id)
                    .Concat(active.Guests).Distinct().Take(plan.Capacity + 3).ToArray();
                return plan;
            }
            long now = Clock.Now.TotalMinutes;
            for (int offset = 0; offset < 14; offset++)
            {
                var date = Clock.Today.AddDays(offset);
                if (profile != RaceMeetCommunity && !date.IsWeekend) continue;
                foreach (int shift in new[] { 0, -60, 60, -120, 120 })
                {
                    long start = date.TotalDays * 1440L + profile.Hour * 60 + shift;
                    if (start < now + 100 || !VenueCommunityOpen(venue, profile, start, plan.Duration + 15)
                        || !CityPeople.Any(p => p.WorkPlaceId == venue.Id && AuthoredCityWorkAt(p, start))) continue;
                    plan.Starts = start;
                    if (findGuests) plan.Guests = VenueSponsorGuests(venue, start, plan.Duration, plan.Capacity + 3);
                    if (!findGuests || plan.Guests.Length >= 3) return plan;
                }
            }
            plan.Starts = -1;
            return plan;
        }

        private string[] VenueSponsorGuests(Place venue, long start, int duration, int limit, string except = null)
        {
            InitializeCityPeople();
            int district = DistrictTierOf(venue);
            var employed = Employees.ToLookup(e => PersonIdOf(e.Person));
            var reserved = _venueSponsorships.Where(a => a.Id != except && !a.Completed && start - 90 < a.Ends + 15 && start + duration + 15 > a.Starts - 90)
                .SelectMany(a => a.Guests).ToHashSet();
            return CityPeople.Where(p => !reserved.Contains(p.PersonId) && p.WorkPlaceId != venue.Id
                    && CityPerson(p.PersonId) is { Age: >= 18 }
                    && !RaceInviteOverlaps(p.PersonId, start - 90, start + duration + 15)
                    && City.HasPlace(p.HomePlaceId) && DistrictTierOf(City.GetPlace(p.HomePlaceId)) <= DistrictAccessTier
                    && !_cityActivities.Any(a => a.Active && start - 90 < a.ScheduledStart + a.Duration
                        && start + duration + 15 > a.ScheduledStart - 90 && a.Participants.Any(x => x.PersonId == p.PersonId)))
                .OrderBy(p => Math.Abs(DistrictTierOf(City.GetPlace(p.HomePlaceId)) - district))
                .ThenByDescending(p => CityPerson(p.PersonId).LeisurePlaces?.Contains(venue.Id) == true)
                .ThenBy(p => p.PersonId, StringComparer.Ordinal)
                .Where(p =>
                {
                    int travel = CityPersonActivityTravel(p, venue, start);
                    return travel <= 90 && (employed.Contains(p.PersonId)
                        ? !employed[p.PersonId].Any(row => EmployeeShiftOverlaps(row.Person, start - travel - 5, start + duration + 15))
                        : !AuthoredCityWorkOverlaps(p, start - travel - 5, start + duration + 15));
                }).Take(limit).Select(p => p.PersonId).ToArray();
        }

        public IReadOnlyList<VenueSponsorProduct> VenueSponsorProducts(OwnedShop shop)
        {
            if (shop == null || ShopAt(shop.Place) != shop) return Array.Empty<VenueSponsorProduct>();
            var stock = shop.Sim.Stock;
            IEnumerable<EventStock> parts = shop.RetailKind == "computer"
                ? stock.Lines().Select(line => new EventStock { Id = line.Part.Id, Boxed = line.Boxed, Count = line.Count, ExpiryDay = -1 })
                : Enumerable.Empty<EventStock>();
            var goods = stock.GoodsLines().Where(line => RetailShopTypes.Sells(shop.RetailKind, line.Key))
                .Select(line => new EventStock { Id = line.Key, Count = line.Value, ExpiryDay = -1 });
            return parts.Concat(goods).Select(item => new VenueSponsorProduct
            {
                Key = (item.Part == null ? "g:" : item.Boxed ? "b:" : "p:") + item.Id,
                Name = item.Name + (item.Part == null ? "" : item.Boxed ? " · 盒装" : " · 散件"),
                Count = Math.Min(VenueSponsorStockLimit, item.Count), Price = EventPrice(shop, item), Stock = item,
            }).Where(item => item.Price.Cents > 1).ToArray();
        }

        public double VenueSponsorPurchaseIntent(OwnedShop shop, VenueSponsorProduct product, Money price) =>
            product == null ? 0 : VenueSponsorPurchaseIntent(shop, product.Stock, product.Price, price);

        private static double VenueSponsorPurchaseIntent(OwnedShop shop, EventStock product, Money normal, Money price)
        {
            if (price <= Money.Zero || price >= normal) return 0;
            double retailIntent = product.Part is PartDef part
                ? shop.Sim.PartPurchaseIntentAtPrice(part, product.Boxed, price.Yuan)
                : shop.Sim.GoodsPurchaseEstimateAtPrice(product.Id, price);
            double discount = 1 - price.Cents / (double)normal.Cents;
            return Math.Clamp(retailIntent * (.22 + .65 * discount), 0, .9);
        }

        public static double VenueSponsorAwareness(int guests) => Math.Round(guests * .12, 1);
        public static double VenueSponsorReputation(int guests) => Math.Round(guests * .06, 1);

        public string BookVenueSponsorship(Place venue, string shopId, int fee, string productKey, int quantity, Money price)
        {
            if (VenueSponsorBlocker(venue) is string reason) return reason;
            var shop = ShopAt(shopId);
            if (shop == null) return "请选择参与的自营门店。";
            var plan = PlanVenueSponsorship(venue, true);
            if (plan.Starts < 0) return "近期找不到足够街坊有空的时段。";
            if (fee < plan.MinimumFee || fee > plan.MaximumFee) return "赞助费超出场所可承办范围。";
            var cost = Money.FromYuan(fee);
            if (!Ledger.CanAfford(cost)) return "可用现金不足。";
            var product = productKey == null ? null : VenueSponsorProducts(shop).FirstOrDefault(p => p.Key == productKey);
            if (productKey != null && (product == null || quantity < 1 || quantity > product.Count || price <= Money.Zero || price >= product.Price))
                return "请检查本店现货、特卖数量和活动价。";

            var pending = ActiveVenueCommunity(venue);
            var record = CreateVenueCommunity(venue, plan, fee);
            if (pending != null)
            {
                record.Id = pending.Id;
                record.PlayerRegistered = pending.PlayerRegistered;
            }
            record.ShopId = shop.Place.Id;
            record.ShopName = ShopTitle(shop);
            record.ShopAccount = shop.SalesAccountId;
            record.FeeCents = cost.Cents;
            if (product != null)
            {
                record.Product = product.Stock.Copy();
                record.Product.Count = quantity;
                record.SaleCents = price.Cents;
                if (record.Product.Part == null)
                {
                    int left = quantity;
                    record.Lots = shop.Sim.Stock.GoodsLots.Where(lot => lot.Id == product.Stock.Id).OrderBy(lot => lot.ExpireDay)
                        .TakeWhile(_ => left > 0).Select(lot =>
                        {
                            var portion = lot.Copy();
                            portion.Count = Math.Min(left, portion.Count);
                            left -= portion.Count;
                            return portion;
                        }).ToArray();
                }
                if (!RemoveEventProduct(shop, record.Product, quantity)) return "门店库存已经变化，请重新选择。";
            }
            if (!Ledger.TryDebit(Clock.Today, "推广", cost, record.Title, VenueSponsorLedger(record, "赞助", shop)))
            {
                ReturnVenueSponsorStock(record, shop, 0);
                return "可用现金不足。";
            }
            if (pending != null) _venueSponsorships[_venueSponsorships.IndexOf(pending)] = record;
            else _venueSponsorships.Add(record);
            Say(record.Title + "已安排，时间已记入日程。");
            return null;
        }

        private static LedgerAttribution VenueSponsorLedger(VenueSponsorship record, string action, OwnedShop shop) =>
            LedgerAttribution.ForShop(shop?.Place.Id ?? record.ShopId, record.Id + ":" + action, action)
                .AssignShopSale(shop?.Place.Id ?? record.ShopId, record.ShopAccount);

        private OwnedShop VenueSponsorShop(VenueSponsorship record) => MyShops.FirstOrDefault(s => s.SalesAccountId == record.ShopAccount);
        private void CancelVenueSponsorshipsForClosedShop(OwnedShop shop)
        {
            foreach (var record in _venueSponsorships.Where(a => !a.Completed && a.ShopAccount == shop.SalesAccountId))
                FinishVenueSponsor(record, null, true);
        }

        private double VenueSponsorInventoryYuan()
        {
            double total = 0;
            foreach (var record in _venueSponsorships)
            {
                if (record.Completed || record.Product is not EventStock item) continue;
                int expired = record.Lots.Where(lot => lot.ExpireDay < Clock.Today.TotalDays).Sum(lot => lot.Count);
                total += (item.Part is PartDef part ? Market.PriceOf(part).Yuan : Gifts.Get(item.Id)?.Price.Yuan ?? 0)
                    * (item.Count - expired);
            }
            return total;
        }

        private void TickVenueSponsorships(long now)
        {
            foreach (var record in _venueSponsorships.Where(a => !a.Completed && now >= a.Starts - 90))
            {
                var shop = VenueSponsorShop(record);
                if (!City.HasPlace(record.VenueId) || record.FeeCents > 0 && shop == null)
                {
                    FinishVenueSponsor(record, shop, true);
                    continue;
                }
                var venue = City.GetPlace(record.VenueId);
                if (record.Running)
                {
                    if (now >= record.Ends) FinishVenueSponsor(record, shop, false);
                    continue;
                }
                bool dispatch = now <= record.Starts && (record.Starts - now) % 5 == 0;
                var duty = dispatch ? Employees.ToLookup(row => PersonIdOf(row.Person)) : null;
                foreach (string id in record.Guests)
                {
                    var person = CityPersonState(id);
                    if (person == null) continue;
                    if (person.ActivityId == record.Id)
                    {
                        if (person.Route == null && person.LocationPlaceId == venue.Id && person.Task == CityPersonTask.Travelling)
                        {
                            var position = new MapPoint(person.X, person.Y);
                            int seat = Array.IndexOf(record.Guests, id) + 1;
                            person.Route = CityActivityLocalRoute(new CityActivity { Id = record.Id, VenueId = venue.Id }, position, seat, now);
                            person.Task = CityPersonTask.Waiting;
                        }
                        continue;
                    }
                    if (!dispatch || person.ActivityId != null || person.VisitId != null) continue;
                    if (duty.Contains(id) ? duty[id].Any(row => EmployeeTrip(row.Person.Id) != null
                            || EmployeeShiftOverlaps(row.Person, now, record.Ends + 15))
                        : AuthoredCityWorkOverlaps(person, now, record.Ends + 15)) continue;
                    var route = PlanCityRoute(person, venue.Id, now, record.Starts - 2);
                    if (route == null || route.ArriveMinute > record.Starts + 10 || route.ArriveMinute < record.Starts - 10) continue;
                    person.ActivityId = record.Id;
                    person.LocationPlaceId = null;
                    person.Route = route;
                    person.Task = CityPersonTask.Travelling;
                    person.TaskLabel = "前往" + record.Title;
                }
                if (now < record.Starts) continue;
                var arrived = PeopleInPlace(venue.Id).Where(p => p.ActivityId == record.Id && p.Task == CityPersonTask.Waiting)
                    .OrderBy(p => Array.IndexOf(record.Guests, p.PersonId)).Take(record.DispatchedCount).ToArray();
                bool ready = VenueHasNpcOnDuty(venue) && VenueCommunityOpen(venue, VenueCommunityAt(venue), now, 1);
                if (now < record.Starts + 15 && (!ready || arrived.Length < record.DispatchedCount)) continue;
                if (!ready || arrived.Length < 3)
                {
                    FinishVenueSponsor(record, shop, true);
                    continue;
                }
                record.Arrived = arrived.Select(p => p.PersonId).ToArray();
                record.Running = true;
                ReleaseVenueSponsorGuests(record, keepArrived: true);
                foreach (var person in arrived)
                {
                    person.Task = CityPersonTask.Participating;
                    person.TaskLabel = record.Title;
                }
                Say($"{record.Title}开始了。{venue.Name}有 {record.Arrived.Length} 位街坊到场。");
            }
        }

        private void ReleaseVenueSponsorGuests(VenueSponsorship record, bool keepArrived)
        {
            foreach (string id in record.Guests)
            {
                var person = CityPersonState(id);
                if (person?.ActivityId != record.Id || keepArrived && record.Arrived.Contains(id)) continue;
                person.ActivityId = null;
                if (person.Route != null) person.LocationPlaceId = null;
                person.Route = null;
                person.TaskLabel = null;
            }
        }

        private void FinishVenueSponsor(VenueSponsorship record, OwnedShop shop, bool cancelled)
        {
            if (record.Completed) return;
            var product = record.Product;
            int sold = 0;
            if (!cancelled && product != null)
            {
                int untracked = product.Count - record.Lots.Sum(lot => lot.Count);
                int available = untracked + record.Lots.Where(lot => lot.ExpireDay >= Clock.Today.TotalDays).Sum(lot => lot.Count);
                Money price = Money.FromCents(record.SaleCents);
                var normal = EventPrice(shop, product);
                record.SalePriceInvalid = price >= normal;
                record.PurchaseIntent = VenueSponsorPurchaseIntent(shop, product, normal, price);
                foreach (string _ in record.Arrived)
                    if (sold < available && Random.Chance(record.PurchaseIntent)) sold++;
                if (sold > 0)
                {
                    Money total = price * sold;
                    long unitCost = product.Part is PartDef part ? Market.PriceOf(part).Cents : Gifts.Get(product.Id)?.Price.Cents ?? 0;
                    Ledger.Credit(Clock.Today, "零售", total, $"{record.Title} · {product.Name} ×{sold}",
                        VenueSponsorLedger(record, "销售", shop).Product(product.Id, sold, unitCost));
                    shop.Sim.RecordOffsiteSales(total);
                    record.RevenueCents = total.Cents;
                }
            }
            ReturnVenueSponsorStock(record, shop, sold);
            if (cancelled && record.FeeCents > 0)
                Ledger.Credit(Clock.Today, "推广退款", Money.FromCents(record.FeeCents), record.Title, VenueSponsorLedger(record, "退款", shop));
            else if (!cancelled && record.FeeCents > 0)
            {
                record.Awareness = VenueSponsorAwareness(record.Arrived.Length);
                record.Reputation = VenueSponsorReputation(record.Arrived.Length);
                shop.Sim.AdjustShopAwareness(record.Awareness);
                shop.Sim.AdjustShopReputation(record.Reputation);
            }
            ReleaseVenueSponsorGuests(record, keepArrived: false);
            record.Sold = sold;
            record.Running = false;
            record.Cancelled = cancelled;
            record.Completed = true;
            if (record.FeeCents > 0)
                Say(cancelled ? record.Title + "未能举办，赞助费已退回。"
                    : $"{record.Title}已结算，{record.ShopName}知名度 +{record.Awareness:0.#}、口碑 +{record.Reputation:0.#}。");
            else Say(cancelled ? record.Title + "未能举行。" : record.Title + "已结束。");
        }

        private void ReturnVenueSponsorStock(VenueSponsorship record, OwnedShop shop, int sold)
        {
            if (record.Product is not EventStock item) return;
            if (item.Part is PartDef part)
            {
                record.Returned = item.Count - sold;
                (shop?.Sim.Stock ?? HomeBoxParts).ForceAdd(part, item.Boxed, record.Returned);
                return;
            }
            void Return(int count, int expiry, bool chilled)
            {
                int used = Math.Min(count, sold);
                sold -= used;
                int left = count - used;
                record.Returned += left;
                if (shop != null) shop.Sim.Stock.ForceAddGoods(item.Id, left, expiry, chilled);
                else HomeBox.AddGift(item.Id, left);
            }
            foreach (var lot in record.Lots)
                if (lot.ExpireDay >= Clock.Today.TotalDays) Return(lot.Count, lot.ExpireDay, lot.Chilled);
                else record.Expired += lot.Count;
            Return(item.Count - record.Lots.Sum(lot => lot.Count), -1, false);
        }

        private void SaveVenueSponsorships(SaveData save) => save.venueSponsorships = _venueSponsorships.Select(a => a.Copy()).ToArray();
        private void RestoreVenueSponsorships(SaveData save)
        {
            _venueSponsorships.Clear();
            _venueSponsorships.AddRange((save.venueSponsorships ?? Array.Empty<VenueSponsorship>())
                .Where(a => a != null && !string.IsNullOrEmpty(a.Id) && (a.FeeCents > 0 || a.PlayerRegistered)).Select(a => a.Copy()));
        }
    }
}
