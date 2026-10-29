using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Market;
using PCShopSell.Core.Shop;
using PCShopSell.Core.Time;
using PCShopSell.Core.Trade;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        public bool CanSave => Trip == null && PendingEvent == null;

        public SaveData CreateSave()
        {
            if (!CanSave) throw new InvalidOperationException("出行途中不能存档。");
            var d = new SaveData
            {
                rngState = Random.State.ToString(System.Globalization.CultureInfo.InvariantCulture),
                day = Clock.Today.TotalDays,
                minute = Clock.MinuteOfDay,
                balanceCents = Ledger.Balance.Cents,
                ledger = Ledger.Entries.Select(e => new SaveLedgerEntry
                {
                    day = e.Date.TotalDays, category = e.Category, cents = e.Amount.Cents, memo = e.Memo,
                    balanceAfter = e.BalanceAfter.Cents,
                }).ToArray(),
                marketFactors = Market.FactorValues(),
                news = Market.News.Select(n => new SaveNews
                {
                    day = n.Date.TotalDays, headline = n.Headline, factor = (int)n.Factor, shock = n.Shock,
                }).ToArray(),
                history = PartCatalog.Categories.Select(c => new SaveHistory
                {
                    category = (int)c, values = Market.History(c).ToArray(),
                }).ToArray(),

                playerNode = PlayerNodeId,
                currentPlace = CurrentPlace?.Id,
                vehicle = (int)Vehicle,
                ownedVehicles = _owned.Select(v => (int)v).ToArray(),
                tricycleImpounded = TricycleImpounded,
                cargo = Lines(Cargo),

                hunger = Player.Hunger,
                energy = Player.Energy,
                health = Player.Health,
                sleepDebt = Player.SleepDebt,
                intelligence = Player.Intelligence,
                charm = Player.Charm,
                skill = Player.Skill,
                lastNewsReadDay = _lastNewsReadDay,

                shopStock = Lines(PlayerShop.Stock),
                markup = PlayerShop.Markup,
                reputation = PlayerShop.Reputation,
                accepted = (int)PlayerShop.Accepted,
                orders = PlayerShop.Orders.Select(o => new SaveOrder
                {
                    id = o.Id, tier = o.Tier, priceCents = o.Price.Cents, placedAt = o.PlacedAt.TotalMinutes,
                    dueAt = o.DueAt.TotalMinutes,
                }).ToArray(),
                nextOrderId = PlayerShop.NextOrderId,

                discoveredStores = Online.DiscoveredIds.ToArray(),
                storeRecords = Online.Records.Select(kv => new SaveStoreRecord
                {
                    store = kv.Key, received = kv.Value.ItemsReceived, defects = kv.Value.Defects,
                }).ToArray(),
                onlineOrders = Online.Pending.Select(o => new SaveOnlineOrder
                {
                    id = o.Id, store = o.StoreId, deliverAt = o.DeliverAtMinute, totalCents = o.TotalCents,
                    lines = o.Lines.Select(l => new SaveStockLine { part = l.PartId, boxed = true, count = l.Count }).ToArray(),
                }).ToArray(),
                nextOnlineOrderId = Online.NextOrderId,
                totalOnlineOrders = Online.TotalOrders,

                bargains = _bargains.Select(kv => new SaveBargain { stall = kv.Key, day = kv.Value.day, bonus = kv.Value.bonus }).ToArray(),

                hasEmployee = Employee != null,
                employee = Employee != null ? ToSave(Employee) : null,
                everHired = EverHired,
                candidates = _candidates.Select(ToSave).ToArray(),
                jobAdDay = JobAdDay,

                monthGoalEvaluated = MonthGoalEvaluated,
                monthGoalPassed = MonthGoalPassed,
                negativeCashDays = NegativeCashDays,
                gameOver = IsGameOver,
                gameOverReason = GameOverReason,
            };
            SaveGrowth(d);
            return d;
        }

        public static GameSession Load(CityLayout city, SaveData d)
        {
            if (d == null) throw new ArgumentNullException(nameof(d));
            ulong rng = ulong.Parse(d.rngState ?? "1", System.Globalization.CultureInfo.InvariantCulture);
            var s = new GameSession(city, rng, Money.Zero, GameTimestamp.From(new GameDate(d.day), d.minute), false);
            s.Restore(d, rng);
            return s;
        }

        private void Restore(SaveData d, ulong rng)
        {
            Random.State = rng;
            Clock.Restore(new GameDate(d.day), d.minute);
            Ledger.Restore(Money.FromCents(d.balanceCents), (d.ledger ?? Array.Empty<SaveLedgerEntry>()).Select(e =>
                new LedgerEntry(new GameDate(e.day), e.category, Money.FromCents(e.cents), e.memo, Money.FromCents(e.balanceAfter))));

            var history = new Dictionary<PartCategory, List<double>>();
            foreach (SaveHistory h in d.history ?? Array.Empty<SaveHistory>())
                history[(PartCategory)h.category] = new List<double>(h.values ?? Array.Empty<double>());
            Market.Restore(d.marketFactors ?? new double[3],
                (d.news ?? Array.Empty<SaveNews>()).Select(n => new NewsItem(new GameDate(n.day), n.headline, (MacroFactor)n.factor, n.shock)),
                history);

            PlayerNodeId = City.Map.HasNode(d.playerNode) ? d.playerNode : Home.NodeId;
            CurrentPlace = d.currentPlace != null && City.HasPlace(d.currentPlace) ? City.GetPlace(d.currentPlace) : null;
            if (CurrentPlace != null && CurrentPlace.NodeId != PlayerNodeId) PlayerNodeId = CurrentPlace.NodeId;
            _owned.Clear();
            foreach (int v in d.ownedVehicles ?? Array.Empty<int>()) _owned.Add((VehicleKind)v);
            TricycleImpounded = d.tricycleImpounded;
            Vehicle = (VehicleKind)d.vehicle;
            Cargo.Clear();
            Cargo.Capacity = CurrentVehicle.CargoCapacity;
            Fill(Cargo, d.cargo);

            Player.Hunger = d.hunger;
            Player.Energy = d.energy;
            Player.Health = d.health;
            Player.SleepDebt = d.sleepDebt;
            Player.Intelligence = d.intelligence;
            Player.Charm = d.charm;
            Player.Skill = d.skill;
            _lastNewsReadDay = d.lastNewsReadDay;

            PlayerShop.Stock.Clear();
            Fill(PlayerShop.Stock, d.shopStock);
            PlayerShop.Markup = d.markup;
            PlayerShop.Reputation = d.reputation;
            PlayerShop.Accepted = (PaymentMethods)d.accepted;
            PlayerShop.RestoreOrders((d.orders ?? Array.Empty<SaveOrder>()).Select(o =>
                new BuildOrder(o.id, o.tier, Money.FromCents(o.priceCents), new GameTimestamp(o.placedAt), new GameTimestamp(o.dueAt))),
                d.nextOrderId);
            PlayerShop.PlayerPresent = CurrentPlace == PlayerShopPlace;

            Online.Restore(d.discoveredStores ?? Array.Empty<string>(),
                (d.storeRecords ?? Array.Empty<SaveStoreRecord>()).Select(r =>
                    new KeyValuePair<string, StoreRecord>(r.store, new StoreRecord { ItemsReceived = r.received, Defects = r.defects })),
                (d.onlineOrders ?? Array.Empty<SaveOnlineOrder>()).Select(o =>
                {
                    var order = new OnlineOrder { Id = o.id, StoreId = o.store, DeliverAtMinute = o.deliverAt, TotalCents = o.totalCents };
                    foreach (SaveStockLine l in o.lines ?? Array.Empty<SaveStockLine>())
                        order.Lines.Add(new OnlineOrderLine { PartId = l.part, Count = l.count });
                    return order;
                }));
            Online.NextOrderId = d.nextOnlineOrderId;
            Online.TotalOrders = d.totalOnlineOrders;

            _bargains.Clear();
            foreach (SaveBargain b in d.bargains ?? Array.Empty<SaveBargain>()) _bargains[b.stall] = (b.day, b.bonus);

            Employee = d.hasEmployee && d.employee != null ? FromSave(d.employee) : null;
            if (Employee != null) PlayerShop.Settings.EmployeeCheckoutMinutes = Employee.CheckoutMinutes;
            EverHired = d.everHired;
            _candidates.Clear();
            foreach (SaveEmployee e in d.candidates ?? Array.Empty<SaveEmployee>()) _candidates.Add(FromSave(e));
            JobAdDay = d.jobAdDay;

            MonthGoalEvaluated = d.monthGoalEvaluated;
            MonthGoalPassed = d.monthGoalPassed;
            NegativeCashDays = d.negativeCashDays;
            IsGameOver = d.gameOver;
            GameOverReason = d.gameOverReason;
            RestoreGrowth(d);
            Trip = null;
            ClearEdge();
        }

        private static SaveStockLine[] Lines(Stock stock) =>
            stock.Lines().Select(l => new SaveStockLine { part = l.Part.Id, boxed = l.Boxed, count = l.Count }).ToArray();

        private static void Fill(Stock stock, SaveStockLine[] lines)
        {
            foreach (SaveStockLine l in lines ?? Array.Empty<SaveStockLine>())
            {
                if (PartCatalog.Exists(l.part)) stock.ForceAdd(PartCatalog.Get(l.part), l.boxed, l.count);
            }
        }

        private static SaveEmployee ToSave(Employee e) => new SaveEmployee
        {
            name = e.Name, wage = e.DailyWageYuan, checkoutMinutes = e.CheckoutMinutes, description = e.Description,
        };

        private static Employee FromSave(SaveEmployee e) => new Employee
        {
            Name = e.name, DailyWageYuan = e.wage, CheckoutMinutes = e.checkoutMinutes, Description = e.description,
        };
    }
}
