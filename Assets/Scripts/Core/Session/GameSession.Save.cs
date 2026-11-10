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
        public bool CanSave => PendingEvent == null && !Racing;

        public SaveData CreateSave()
        {
            if (!CanSave) throw new InvalidOperationException("请先处理当前待选择事件再存档。");
            QuestIndex(); // 固化已完成教学，再序列化 flags。
            OwnedShop keep = _focus;
            _focus = MainShop;
            try
            {
                SaveData d = CreateSaveOfMain();
                d.districtProgress = _district.Copy();
                d.extraShops = _shops.Skip(1).Where(s => s.Rented).Select(SaveShopOf).ToArray();
                d.focusShop = Math.Max(0, _shops.IndexOf(keep));
                SaveCityFinance(d);
                SaveCityProcurement(d);
                SaveCityAssets(d);
                SaveCityRetail(d);
                SaveCityLife(d);
                SaveCityClothing(d);
                SaveCityClimate(d);
                SaveHomeWorkbench(d);
                d.venueBusinesses=MyShops.Where(s=>IsDedicatedVenue(s.Place)).Select(s=>VenueOperation(s.Place.Id).Copy()).ToArray();
                SaveAdditionalStaff(d);
                SaveSpecialBusinesses(d);
                SavePetBoarding(d);
                SaveRacing(d);
                return d;
            }
            finally
            {
                _focus = keep;
            }
        }

        /// <summary>主店的数据还按原来的字段存（老存档也是这么读的）。</summary>
        private SaveData CreateSaveOfMain()
        {
            if (_focus.AutoBuild) Flags.Add(AutoBuildFlag); else Flags.Remove(AutoBuildFlag);
            var d = new SaveData
            {
                version=2,
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
                ownedVehicles = _garage.Select(v => (int)MotorCatalog.Get(v.ModelId).Kind).ToArray(),
                tricycleImpounded = TricycleImpounded,
                impoundedTricycles = ImpoundedVehicleCount(VehicleKind.Tricycle),
                impoundedEBikes = ImpoundedVehicleCount(VehicleKind.EBike),
                hasBuyer = _focus.Buyer != null,
                hasSalesperson = _focus.Salesperson != null,
                salesperson = _focus.Salesperson != null ? ToSave(_focus.Salesperson) : null,
                buyer = _focus.Buyer != null ? ToSave(_focus.Buyer) : null,
                employeeTrips = _employeeTrips.Select(t => t.Copy()).ToArray(),
                employeePurchaseHistory = _employeePurchaseHistory.Select(t => t.Copy()).ToArray(),
                warehouseAccessVersion = 1,
                idleEmployees = _idleEmployees.Select(ToSave).ToArray(),
                cargo = Lines(Cargo),

                playerWork = WorkContract?.Copy(),
                weightUnitsVersion = 1,
                hasDietStats = true, nutrition = Player.Nutrition, mood = Player.Mood,
                hunger = Player.Hunger,
                energy = Player.Energy,
                health = Player.Health,
                sleepDebt = Player.SleepDebt,
                sleepSpans = _sleepHistory.SelectMany(span => span).ToArray(),
                intelligence = Player.Intelligence,
                charm = Player.Charm,
                skill = Player.Skill,
                playerStress = Player.Stress, hasPlayerWorkAttributes = true,
                playerStamina = Player.Stamina,
                lastNewsReadDay = _lastNewsReadDay,

                shopStock = Lines(PlayerShop.Stock),
                shopRetailKind = MainShop.RetailKind, pendingRetailKind = PendingRetailKind, shopCustomName = MainShop.CustomName,
                shopGoodsLots = SaveRetailLots(PlayerShop),
                shopGoodsIds = SaveRetailCounts(PlayerShop).Select(kv => kv.Key).ToArray(),
                shopGoodsCounts = SaveRetailCounts(PlayerShop).Select(kv => kv.Value).ToArray(),
                markup = PlayerShop.Markup,
                shopOpenMinute = PlayerShop.OpenMinute,
                shopCloseMinute = PlayerShop.CloseMinute,
                reputation = PlayerShop.Reputation,
                hasShopStanding = true,
                shopAwareness = PlayerShop.Awareness,
                shopReputation = PlayerShop.ShopReputation,
                accepted = (int)PlayerShop.Accepted,
                orders = PlayerShop.Orders.Select(o => new SaveOrder
                {
                    id = o.Id, contractId = o.ContractId, tier = o.Tier, priceCents = o.Price.Cents, placedAt = o.PlacedAt.TotalMinutes,
                    dueAt = o.DueAt.TotalMinutes, partsReserved = o.PartsReserved, mistakes = o.Mistakes, employeeMinutesLeft = o.EmployeeMinutesLeft,
                }).ToArray(),
                nextOrderId = PlayerShop.NextOrderId,
                displayPcs = PlayerShop.DisplayPcs.ToArray(),

                discoveredStores = Online.DiscoveredIds.ToArray(),
                storeRecords = Online.Records.Select(kv => new SaveStoreRecord
                {
                    store = kv.Key, received = kv.Value.ItemsReceived, defects = kv.Value.Defects,
                }).ToArray(),
                onlineOrders = Online.Pending.Select(o => new SaveOnlineOrder
                {
                    id = o.Id, store = o.StoreId, deliverAt = o.DeliverAtMinute, totalCents = o.TotalCents, toHome = o.ToHome, shopId = o.ShopId,
                    lines = o.Lines.Select(l => new SaveStockLine { part = l.PartId, boxed = true, count = l.Count }).ToArray(),
                }).ToArray(),
                nextOnlineOrderId = Online.NextOrderId,
                totalOnlineOrders = Online.TotalOrders,

                bargains = _bargains.Select(kv => new SaveBargain { stall = kv.Key, day = kv.Value.day, bonus = kv.Value.bonus }).ToArray(),

                hasEmployee = Employee != null,
                employee = Employee != null ? ToSave(Employee) : null,
                hasAssembler = Assembler != null,
                assembler = Assembler != null ? ToSave(Assembler) : null,
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
            d.appLoans = _appLoans.Select(l => l.Copy()).ToArray();
            d.bankDepositCents = _bankDepositCents; d.bankInterestCents = _bankInterestCents; d.bankFraction = _bankFraction;
            d.priceDays = _priceDays.Select(p => p.Copy()).ToArray();
            SavePropertyMarket(d);
            d.economicReportCadence = (int)ReportCadence;
            d.weeklyPricesPending = EconomicReportPending;
            SaveRealty(d);
            SaveBusiness(d);
            d.mcnIntroductions=_mcnIntroductions.Select(x=>x.Copy()).ToArray();
            d.residentialHomeId=_residentialHomeId;
            d.residentialLeases=SaveResidentialLeases();
            d.kolStreaming=_kol.Copy();
            return d;
        }

        public static GameSession Load(CityLayout city, SaveData d)
        {
            if (d == null) throw new ArgumentNullException(nameof(d));
            MigrateWeightUnits(d);
            ulong rng = ulong.Parse(d.rngState ?? "1", System.Globalization.CultureInfo.InvariantCulture);
            var s = new GameSession(city, rng, Money.Zero, GameTimestamp.From(new GameDate(d.day), d.minute), false, !d.noPhone);
            s.Restore(d, rng);
            return s;
        }

        private void Restore(SaveData d, ulong rng)
        {
            Random.State = rng;
            Clock.Restore(new GameDate(d.day), d.minute);
            RestorePropertyMarket(d);
            Ledger.Restore(Money.FromCents(d.balanceCents), (d.ledger ?? Array.Empty<SaveLedgerEntry>()).Select(e =>
                new LedgerEntry(new GameDate(e.day), e.category, Money.FromCents(e.cents), e.memo, Money.FromCents(e.balanceAfter))));
            LoadCityFinance(d);

            var history = new Dictionary<PartCategory, List<double>>();
            foreach (SaveHistory h in d.history ?? Array.Empty<SaveHistory>())
                history[(PartCategory)h.category] = new List<double>(h.values ?? Array.Empty<double>());
            Market.Restore(d.marketFactors ?? new double[3],
                (d.news ?? Array.Empty<SaveNews>()).Select(n => new NewsItem(new GameDate(n.day), n.headline, (MacroFactor)n.factor, n.shock)),
                history);

            PlayerNodeId = City.Map.HasNode(d.playerNode) ? d.playerNode : Home.NodeId;
            CurrentPlace = d.currentPlace != null && City.HasPlace(d.currentPlace) ? City.GetPlace(d.currentPlace) : null;
            if (CurrentPlace != null && CurrentPlace.NodeId != PlayerNodeId) PlayerNodeId = CurrentPlace.NodeId;
            LoadRacing(d);
            Vehicle = (VehicleKind)d.vehicle;
            if (!CanUse(Vehicle)) Vehicle = VehicleKind.Walk;
            Cargo.Clear();
            Cargo.Capacity = CurrentVehicle.CargoCapacity;
            Fill(Cargo, d.cargo);

            Player.Nutrition = d.hasDietStats ? Math.Max(0, Math.Min(100, d.nutrition)) : 50;
            Player.Mood = d.hasDietStats ? Math.Max(0, Math.Min(100, d.mood)) : 50;
            Player.Hunger = d.hunger;
            Player.Energy = d.energy;
            Player.Health = d.health;
            Player.SleepDebt = d.sleepDebt;
            RestoreSleepHistory(d.sleepSpans);
            Player.Intelligence = d.intelligence;
            Player.Charm = d.charm;
            Player.Skill = d.skill;
            Player.ChangeStress(d.playerStress - Player.Stress);
            Player.Stamina = d.hasPlayerWorkAttributes ? Math.Max(0, Math.Min(100, d.playerStamina)) : 50;
            _lastNewsReadDay = d.lastNewsReadDay;

            RestoreRealty(d);
            _focus.RetailKind = HasShop
                ? PCShopSell.Core.Home.RetailShopTypes.Valid(string.IsNullOrEmpty(d.shopRetailKind) ? "computer" : d.shopRetailKind)
                : PCShopSell.Core.Home.RetailShopTypes.Default;
            _focus.CustomName = _focus.Rented ? d.shopCustomName : null;
            PlayerShop.RetailKind = _focus.RetailKind;
            PendingRetailKind = PCShopSell.Core.Home.RetailShopTypes.Valid(d.pendingRetailKind);
            CancelProtectedPendingLease();
            PlayerShop.Stock.Clear();
            Fill(PlayerShop.Stock, d.shopStock);
            string[] goodsIds = d.shopGoodsIds ?? Array.Empty<string>();
            for (int i = 0; i < goodsIds.Length && d.shopGoodsCounts != null && i < d.shopGoodsCounts.Length; i++)
                PlayerShop.Stock.ForceAddGoods(goodsIds[i], d.shopGoodsCounts[i]);
            PlayerShop.Stock.RestoreGoodsLots(d.shopGoodsLots, Clock.Today.TotalDays);
            PlayerShop.Markup = d.markup;
            if (d.shopCloseMinute > 0) PlayerShop.SetHours(d.shopOpenMinute, d.shopCloseMinute);
            PlayerShop.RestoreBusinessAwareness(d.reputation);
            PlayerShop.RestoreShopStanding(d.hasShopStanding ? d.shopAwareness : d.reputation,
                d.hasShopStanding ? d.shopReputation : ShopSimulation.NeutralShopReputation);
            PlayerShop.Accepted = (PaymentMethods)d.accepted;
            PlayerShop.RestoreOrders((d.orders ?? Array.Empty<SaveOrder>()).Select(o =>
                new BuildOrder(o.id, o.tier, Money.FromCents(o.priceCents), new GameTimestamp(o.placedAt), new GameTimestamp(o.dueAt)) { ContractId = o.contractId, PartsReserved = o.partsReserved, Mistakes = Math.Max(0, o.mistakes), EmployeeMinutesLeft = Math.Max(0, o.employeeMinutesLeft) }),
                d.nextOrderId);
            PlayerShop.RestoreDisplay(d.displayPcs);
            PlayerShop.PlayerPresent = InPlayerShop;

            Online.Restore(d.discoveredStores ?? Array.Empty<string>(),
                (d.storeRecords ?? Array.Empty<SaveStoreRecord>()).Select(r =>
                    new KeyValuePair<string, StoreRecord>(r.store, new StoreRecord { ItemsReceived = r.received, Defects = r.defects })),
                (d.onlineOrders ?? Array.Empty<SaveOnlineOrder>()).Select(o =>
                {
                    var order = new OnlineOrder { Id = o.id, StoreId = o.store, DeliverAtMinute = o.deliverAt, TotalCents = o.totalCents, ToHome = o.toHome, ShopId = string.IsNullOrEmpty(o.shopId) ? null : o.shopId };
                    foreach (SaveStockLine l in o.lines ?? Array.Empty<SaveStockLine>())
                        order.Lines.Add(new OnlineOrderLine { PartId = l.part, Count = l.count });
                    return order;
                }));
            Online.NextOrderId = d.nextOnlineOrderId;
            Online.TotalOrders = d.totalOnlineOrders;

            _bargains.Clear();
            foreach (SaveBargain b in d.bargains ?? Array.Empty<SaveBargain>()) _bargains[b.stall] = (b.day, b.bonus);

            Employee = d.hasEmployee && d.employee != null ? FromSave(d.employee) : null;
            if (Employee != null) PlayerShop.Settings.EmployeeCheckoutMinutes = EmployeeCheckoutCycleMinutes(Employee) - 1;
            Assembler = !d.hasAssembler || d.assembler == null ? null : FromSave(d.assembler);
            _focus.Buyer = d.hasBuyer && d.buyer != null ? FromSave(d.buyer) : null;
            _focus.Salesperson = d.hasSalesperson && d.salesperson != null ? FromSave(d.salesperson) : null;
            _idleEmployees.Clear();
            _idleEmployees.AddRange((d.idleEmployees ?? Array.Empty<SaveEmployee>()).Select(FromSave));
            _employeeTrips.Clear();
            _employeeTrips.AddRange((d.employeeTrips ?? Array.Empty<EmployeePurchaseTrip>()).Select(t => t.Copy()));
            _employeePurchaseHistory.Clear();
            _employeePurchaseHistory.AddRange((d.employeePurchaseHistory ?? Array.Empty<EmployeePurchaseTrip>()).Where(t=>t!=null).Select(t=>t.Copy()));
            EverHired = d.everHired;
            _candidates.Clear();
            foreach (SaveEmployee e in d.candidates ?? Array.Empty<SaveEmployee>())
            {
                var candidate = FromSave(e);
                if (candidate.CandidateLeavesAt <= 0) candidate.CandidateLeavesAt = (long)Clock.Today.TotalDays * 1440 + CandidateLeaveMinute;
                _candidates.Add(candidate);
            }
            ExpireCandidates();
            JobAdDay = d.jobAdDay;

            MonthGoalEvaluated = d.monthGoalEvaluated;
            MonthGoalPassed = d.monthGoalPassed;
            NegativeCashDays = d.negativeCashDays;
            IsGameOver = d.gameOver;
            GameOverReason = d.gameOverReason;
            RestoreGrowth(d);
            LoadCityClothing(d);
            RestorePlayerWork(d.playerWork);
            Flags.Remove("employee:trained");
            _focus.AutoBuild = Flags.Contains(AutoBuildFlag);
            RestoreBusiness(d);
            _residentialHomeId=City.HasPlace(d.residentialHomeId)&&City.GetPlace(d.residentialHomeId).Kind==PlaceKind.Home?d.residentialHomeId:null;
            RestoreHomeLease(d);
            RestorePriceReports(d);
            RestoreAppLoans(d);
            RestoreBank(d);
            foreach (SaveShop shop in d.extraShops ?? Array.Empty<SaveShop>()) RestoreShop(shop, d.hasShopStanding);
            LoadAdditionalStaff(d);
            foreach (OwnedShop shop in _shops) ClearUnstaffedAssembly(shop);
            LoadCityAssets(d);
            LoadResidentialLeases(d);
            if (d.shopFootfallVersion < 2) MigrateWeeklyCommercialRent();
            LoadCityProcurement(d);
            LoadCityRetail(d);
            LoadCityClimate(d);
            foreach (OwnedShop s in _shops) GiveStarterBoxes(s);
            OwnedShop inside = ShopAt(CurrentPlace);
            OwnedShop focus = inside ?? (d.focusShop > 0 && d.focusShop < _shops.Count ? _shops[d.focusShop] : null);
            if (focus != null && focus != _focus)
            {
                _focus = focus;
                ApplyUpgrades();
            }
            foreach (OwnedShop s in _shops) s.Sim.PlayerPresent = s.Place == CurrentPlace && s.Rented;
            Trip = null;
            ClearEdge();
            LoadCityLife(d);
            InitializeExpansionResidents();
            LoadVenueBusinesses(d);
            LoadEnergyPayroll(d);
            MigrateStoreMainCategories();
            MigrateCityGeometry(d);
            NormalizeCityWalkingRoutes();
            NormalizeWarehouses();
            MigrateWarehouseBoxAccess(d);
            LoadSpecialBusinesses(d);
            LoadPetBoarding(d);
            LoadWeeklySupplierPlans();
            LoadHomeWorkbench(d);
            LoadDistrictProgress(d);
            RestoreMcnIntroductions(d);
            RestoreKolStreaming(d);
            RefreshBrandTraffic();
            EnsureFirstShopListings(Clock.Today.TotalDays);
        }

        private const string AutoBuildFlag = "employee:auto-build";

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
            candidateLeavesAt = e.CandidateLeavesAt,
            hasCharm = true, employeeCharm = EmployeeCharm(e),
            hasEmployeeIntelligence = true, employeeIntelligence = e.Intelligence,
            hasEmployeeWorkAttributes = true, dexterity = e.Dexterity, stamina = e.Stamina,
            id = e.Id, transferShopId = e.TransferShopId, transferDay = e.TransferDay,
            hasTransferRole = e.TransferRole >= 0, transferRole = e.TransferRole,
            hasWorkSkills = true, assemblyMinutes = e.AssemblyMinutes, purchaseMinutes = e.PurchaseMinutes,
            purchasePlan = e.PurchasePlan?.Copy(), lastPurchaseNote = e.LastPurchaseNote,
            schedule = e.Schedule?.Copy(),
            hasSchedule = true,
            name = e.Name, wage = e.DailyWageYuan, checkoutMinutes = e.CheckoutMinutes, description = e.Description, shiftStart = e.ShiftStart, shiftEnd = e.ShiftEnd, buildsCompleted = e.BuildsCompleted,
        };

        private static Employee FromSave(SaveEmployee e) => new Employee
        {
            CandidateLeavesAt = e.candidateLeavesAt,
            Intelligence = e.hasEmployeeIntelligence ? Math.Max(0,Math.Min(100,e.employeeIntelligence)) : 50,
            Dexterity = e.hasEmployeeWorkAttributes ? Math.Max(0, Math.Min(100, e.dexterity)) : 50,
            Stamina = e.hasEmployeeWorkAttributes ? Math.Max(0, Math.Min(100, e.stamina)) : 50,
            Charm = e.hasCharm ? Math.Max(0,Math.Min(100,e.employeeCharm)) : -1,
            Id = string.IsNullOrEmpty(e.id) ? Guid.NewGuid().ToString("N") : e.id, TransferShopId = e.transferShopId, TransferDay = e.transferDay,
            TransferRole = e.hasTransferRole ? e.transferRole : -1,
            AssemblyMinutes = e.hasWorkSkills ? Math.Max(45, e.assemblyMinutes) : 90,
            PurchaseMinutes = e.hasWorkSkills ? Math.Max(1, e.purchaseMinutes) : 2,
            PurchasePlan = e.purchasePlan?.Copy() ?? new EmployeePurchasePlan(), LastPurchaseNote = e.lastPurchaseNote,
            Name = e.name, DailyWageYuan = e.wage, CheckoutMinutes = e.checkoutMinutes, Description = e.description,
            Schedule = e.hasSchedule ? e.schedule?.Copy() ?? new EmployeeSchedule() : new EmployeeSchedule(),
            ShiftStart = e.shiftStart>=0&&e.shiftStart<1440&&e.shiftEnd>=0&&e.shiftEnd<1440&&e.shiftStart!=e.shiftEnd ? e.shiftStart : DutyStartMinute,
            ShiftEnd = e.shiftStart>=0&&e.shiftStart<1440&&e.shiftEnd>=0&&e.shiftEnd<1440&&e.shiftStart!=e.shiftEnd ? e.shiftEnd : DutyEndMinute, BuildsCompleted = Math.Max(0, e.buildsCompleted),
        };
    }
}
