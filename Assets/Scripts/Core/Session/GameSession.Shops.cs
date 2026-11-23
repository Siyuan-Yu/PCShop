using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Shop;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    public enum ShopGuestEvent { Entered, LeftWithoutPaying, Paid }

    /// <summary>
    /// 同时经营几家自营电脑店。每家店的货、货架、员工、升级、补货目标各管各的，知名度和现金共用。
    /// 会话里 PlayerShop / Lease / PlayerShopPlace / Employee 等指的都是“当前这家店”：人进了哪家店就切到哪家，
    /// 出门后保持最后进过的那家。不在当前的店照常营业（有员工值守才开门）。
    /// </summary>
    public sealed partial class GameSession
    {
        private static readonly string[] Digits = { "零", "一", "二", "三", "四", "五", "六", "七", "八", "九" };

        /// <summary>第 n 家店的中文序号（2 → 二，10 → 十，12 → 十二，23 → 二十三）。</summary>
        private static string ChineseNumber(int n)
        {
            if (n < 10) return Digits[n];
            if (n < 100) return (n / 10 == 1 ? "" : Digits[n / 10]) + "十" + (n % 10 == 0 ? "" : Digits[n % 10]);
            return n.ToString();
        }

        /// <summary>任何一家自营店来了顾客（街上让路人走进那家店的门）。</summary>
        public event Action<Place, ShopCustomer> ShopCustomerArrived;
        public event Action<Place, ShopGuestEvent> ShopGuestChanged;

        /// <summary>已经租下的自营店，第一家是主店。</summary>
        public IReadOnlyList<OwnedShop> MyShops => _shops.Where(s => s.Rented).ToList();

        public int MyShopCount => _shops.Count(s => s.Rented);

        /// <summary>当前这家店（还没租铺子时是一个占位，Rented 为 false）。</summary>
        public OwnedShop CurrentShop => _focus;

        public OwnedShop MainShop => _shops[0];

        public OwnedShop ShopAt(Place p) => p == null ? null : _shops.Find(s => s.Rented && s.Place == p);

        public OwnedShop ShopAt(string placeId) => _shops.Find(s => s.Rented && s.Place.Id == placeId);

        public const int MaxShopNameLength = 16;

        public string ShopTitle(OwnedShop s)
        {
            if (!string.IsNullOrEmpty(s.CustomName)) return s.CustomName;
            if(IsDedicatedVenue(s.Place))return "我的"+VenueBusinessName(s.Place);
            int i = _shops.IndexOf(s);
            return "我的" + RetailShopTypes.Name(s.RetailKind) + (i > 0 ? "·" + ChineseNumber(i + 1) + "店" : "");
        }

        public bool TryRenameShop(OwnedShop shop, string name, out string reason)
        {
            name = name?.Trim() ?? "";
            if (IsGameOver || shop == null || ShopAt(shop.Place) != shop || CurrentPlace != shop.Place)
                reason = "请在自己的店铺柜台修改店名。";
            else if (name.Length == 0 || name.Length > MaxShopNameLength)
                reason = $"店名需要 1 至 {MaxShopNameLength} 个字符。";
            else if (name.Any(c => char.IsControl(c) || c == '<' || c == '>'))
                reason = "店名不能包含换行或尖括号。";
            else
            {
                shop.CustomName = name;
                reason = null;
            }
            return reason == null;
        }

        /// <summary>开了不止一家时，提示里带上是哪家店。</summary>
        private string ShopTag => MyShopCount > 1 ? $"（{ShopTitle(_focus)}）" : "";

        public const int BuildOrdersStartDay = 14;

        private ShopSimulation CreateShopSim(OwnedShop shop)
        {
            var sim = new ShopSimulation(shop.Place.Id, shop.Place.Size, Clock, Ledger, Market, Random)
            {
                TradingEnabled = !IsDedicatedVenue(shop.Place) && shop.RetailKind!="restaurant",
                CheckoutClock = ()=>CurrentPlace==shop.Place&&!_sleeping&&!IsWorking?_activeCheckoutSeconds:double.NaN,
                RetailKind = shop.RetailKind,
                LocationFactor = LocationFactorOf(shop.Place),
                CompetitionFactorOf = () => ShopCompetitionFactor(shop.Place),
                StreetTrafficFactorOf = () => StreetTrafficMultiplier(shop.Place.StreetId)*NeighborhoodShopTraffic(shop.Place.Id),
                OwnerOnSiteOf = () => shop.Rented && CurrentPlace == shop.Place && !IsGameOver,
                EmployeeOnDuty = t => ShopStaff(shop,EmployeeRole.Cashier).Concat(ShopStaff(shop,EmployeeRole.Assembler)).Any(e=>e.OnDuty(t)&&CityEmployeeAvailable(e,shop)),
                EmployeeServiceRate = ()=>ShopCheckoutWorkers(shop).Sum(e=>1.0/EmployeeCheckoutCycleMinutes(e)),
            };
            sim.OpenStatusOf=t=>IsSpecialBusiness(shop)?SpecialOpen(shop,t):sim.IsOpenAt(t);
            sim.PowerTrafficOf = t => ShopPowerTraffic(shop, t);
            sim.FinalSalePrice = c => MemberSalePrice(shop, c);
            sim.TemporaryPriceFactorOf = id => EventPriceFactor(shop, id);
            sim.Settings.BuildOrdersStartDay = BuildOrdersStartDay;
            sim.SalesBonusOf = () => ShopSalesBonus(shop);
            sim.HasClothingMirror = () => shop.Layout.Any(p=>FurnitureCatalog.Canonical(p.Id)==FurnitureCatalog.FittingMirror);
            sim.WorkMinute = now => WithShop(shop, () => UpdateEmployeeWork(now));
            sim.EmployeeBusy = () => shop.EmployeeBuilding;
            sim.DayClosed += r =>
            {
                if (shop.Rented) Notice?.Invoke((MyShopCount > 1 ? ShopTitle(shop) + " " : "") + DayReportText(r));
            };
            sim.CustomerArrived += c =>
            {
                BindCityCustomer(shop, c);
                if (RecordBrandShopVisit(shop, c)) ShopGuestChanged?.Invoke(shop.Place, ShopGuestEvent.Entered);
                ShopCustomerArrived?.Invoke(shop.Place, c);
            };
            sim.CustomerCanBrowse = c =>
            {
                bool inside = RecordBrandShopVisit(shop, c);
                if (inside)
                {
                    OfferModelCommission(shop, c);
                    ShowReferralVisit(shop, c);
                }
                return inside;
            };
            sim.RetailTemperature=()=>CityOutdoorTemperature(Clock.Now.TotalMinutes);
            sim.RetailInterestOf=c=>CityRetailSeriesFor(c.PersonId ?? c.VisitId ?? (shop.Place.Id + ":" + c.Id));
            sim.RetailBudgetOf=c=>CityRetailBudgetFor(c.PersonId ?? c.VisitId ?? (shop.Place.Id + ":" + c.Id));
            sim.CustomerReadyToPay += c => ReserveFlashPrice(shop, c);
            sim.CustomerLeft += c =>
            {
                if (c.Reason != LeaveReason.OrderPlaced && CityCustomerIsInside(c, shop.Place.Id))
                    ShopGuestChanged?.Invoke(shop.Place, ShopGuestEvent.LeftWithoutPaying);
                ReleaseFlashPrice(shop, c);
                ShowReferralOutcome(shop, c, false);
                ReleaseCityCustomer(c);
                WithShop(shop, () => OnShopCustomerLeft(c));
            };
            sim.ResaleOffers=()=>UsedOffers(shop);
            sim.CustomerPaid += c =>
            {
                ShowReferralOutcome(shop, c, true);
                ReleaseCityCustomer(c);
                CompleteUsedSale(shop, c);
                RecordEventPaidReceipt(shop, c);
                RecordFinanceSale(shop, c);
                ShopGuestChanged?.Invoke(shop.Place, ShopGuestEvent.Paid);
            };
            sim.BulkSalePaid += (id, name, total) => RecordAggregateReceipt(shop, "bulk:" + id, name, total, Clock.Today.TotalDays);
            sim.OrderCompleted += o =>
            {
                Flags.Add("tutorial:build");
                ContractOrderResult(shop, o, true);
                ShopGuestChanged?.Invoke(shop.Place, ShopGuestEvent.Paid);
            };
            sim.OrderCancelled += o => ContractOrderResult(shop, o, false);
            sim.OrderExpired += o => ContractOrderResult(shop, o, false);
            sim.OrderPlaced += o => Notice?.Invoke($"{(MyShopCount > 1 ? ShopTitle(shop) : "")}新装机订单：{o.ProductName} {o.Price}，{sim.Settings.BuildOrderDueDays} 天内交付。");
            sim.OrderExpired += o =>
            {
                if (o.ContractId == null)
                    Popup?.Invoke("订单取消", $"{(MyShopCount > 1 ? ShopTitle(shop) + "的" : "")}{o.ProductName}订单逾期，顾客已取消。逾期会影响门店口碑。");
            };
            if (_shops.Count > 0 && _shops[0] != shop && _shops[0].Sim != null) sim.ShareReputationWith(_shops[0].Sim);
            return sim;
        }

        /// <summary>临时把“当前店”切到 s 做一件事（后台店的员工干活、自动补货、到货入库），做完切回来。</summary>
        private void WithShop(OwnedShop s, Action a)
        {
            if (s == null || s == _focus)
            {
                a();
                return;
            }
            OwnedShop keep = _focus;
            _focus = s;
            try { a(); }
            finally { _focus = keep; }
        }

        /// <summary>切换当前店（进了另一家自己的店）：店内场景按这家重建。</summary>
        private void SetFocus(OwnedShop s)
        {
            if (s == null || s == _focus) return;
            _focus = s;
            _layoutUndo.Clear();
            _layoutRedo.Clear();
            ApplyUpgrades();
            ShopExpanded?.Invoke(s.Sim.Size);
        }

        /// <summary>送到“店里”的货该进哪家店的库存：下单时的那家；那家退租了就进主店。</summary>
        private OwnedShop DeliveryShop(string placeId) => (placeId != null ? ShopAt(placeId) : null) ?? (MainShop.Rented ? MainShop : _focus);

        /// <summary>已经有店再签一间：开一家新店（不搬旧店）。新店是毛坯，变成当前店。</summary>
        private void OpenNewShop(Place place, Money rent, Money deposit)
        {
            var shop = new OwnedShop { Place = place };
            shop.Lease = new ShopLease { PlaceId = place.Id, Rent = rent, Deposit = deposit, StartDay = Clock.Today.TotalDays, SignedDay = Clock.Today.TotalDays };
            shop.Sim = CreateShopSim(shop);
            shop.Sim.Markup = PlayerShop.Markup;
            _shops.Add(shop);
            _vacant.Remove(place.Id);
            _listings.RemoveAll(x => x.PlaceId == place.Id);
            GiveStarterBoxes(shop);
            SetFocus(shop);
        }

        /// <summary>退租与结束自营共用办理条件。</summary>
        public string CloseShopBlocker(OwnedShop s)
        {
            if (s == null || ShopAt(s.Place) != s) return "这不是你的店";
            if (IsGameOver) return "当前无法办理";
            if (CurrentPlace != s.Place && !InAgency) return "要在这家店里或所属片区中介办理退租";
            if (InAgency && !AgencyServes(CurrentPlace,s.Place)) return "请到店铺所属片区的中介办理退租";
            if (CurrentPlace == s.Place && LeaveBlocker is string leave) return leave.TrimEnd('。');
            if (IsDedicatedVenue(s.Place) && VenueOperation(s.Place.Id)?.CustomersHere.Any(c => c.Started >= 0) == true)
                return "还有顾客在使用场馆，请先停止接客并等待结束";
            if(RunningCityActivity?.VenueId==s.Place.Id)return "请先结束自己的场馆活动";
            if (ModelCommissionsBusy(s)) return "还有客带模型在店，请先交付或归还";
            if (_cityReferrals.Any(offer => offer.ShopId == s.Place.Id && offer.ContractId != null && !offer.Settled))
                return "还有没交付的转介绍订单";
            if (_specialBusinesses.TryGetValue(s.Place.Id, out var business)
                && business.Orders.Any(o => o.Stage != DiningStage.Completed && o.Stage != DiningStage.Left))
                return "还有餐馆订单，请先处理完";
            if(PetsStaying(s))return "还有宠物在店代养，请先交接结算再结束经营";
            return null;
        }

        /// <summary>结束门店经营，清空门店资产，将本店员工转为待安排。</summary>
        public bool CloseShop(string placeId)
        {
            OwnedShop s = ShopAt(placeId);
            string why = CloseShopBlocker(s);
            if (why != null) return Fail(why + "。");
            string title = ShopTitle(s);
            if (CurrentPlace == s.Place)
            {
                Leave();
                if (CurrentPlace == s.Place) return false;
            }
            s.Sim.ClearCustomers();
            foreach (var order in s.Sim.Orders.ToArray()) s.Sim.CancelBuild(order.Id);
            double reputation = MainShop.Sim.Reputation;
            RedirectClosedShopDeliveries(s);
            foreach (var claim in _cityProcurementClaims.Where(claim => claim.ShopId == placeId)) CancelProcurementClaim(claim.Id);
            var assigned = Employees;
            RetireCompanyShop(s);
            int releasedStaff = 0;
            foreach (var row in assigned)
            {
                if (row.Shop == s)
                {
                    MoveEmployeeWork(row, null, EmployeeRole.Unassigned);
                    if (row.Person.Schedule != null) row.Person.Schedule.WorkTravelTarget = null;
                    if (row.Person.PurchasePlan != null) row.Person.PurchasePlan.Daily = false;
                    releasedStaff++;
                }
                else if (row.Person.TransferShopId == placeId) CancelEmployeeTransfer(row.Person.Id);
            }
            Money lostDeposit=s.Lease?.Deposit??Money.Zero;
            Money rent = s.Lease?.Rent ?? Money.Zero;
            bool wasMain = s == MainShop;
            bool wasOwned = s.OwnsProperty;
            _shops.Remove(s);
            _petBoarding.Remove(placeId);
            _modelCommissions.Remove(placeId);
            _specialBusinesses.Remove(placeId);
            _eventReferrals.RemoveAll(visit => visit.ShopId == placeId);
            _flyerCampaigns.RemoveAll(campaign => campaign.ShopId == placeId);
            _eventFlashSales.RemoveAll(sale => sale.ShopId == placeId);
            _eventFlashReservations.RemoveAll(reservation => reservation.ShopId == placeId);
            foreach(var id in _employeeHireShops.Where(p=>p.Value==placeId).Select(p=>p.Key).ToArray())_employeeHireShops.Remove(id);
            ResetVenueLease(placeId);
            s.Sim.Dispose();
            var property = PropertyAt(placeId);
            if (property != null) { property.UseMode = "vacant"; property.TenantName = ""; }
            if (_shops.Count == 0)
            {
                var placeholder = new OwnedShop { Place = City.PlayerShop() };
                placeholder.Sim = CreateShopSim(placeholder);
                _shops.Add(placeholder);
            }
            if (wasMain)
            {
                ShopSimulation main = MainShop.Sim;
                main.ShareReputationWith(main);
                main.RestoreBusinessAwareness(reputation);
                foreach (OwnedShop o in _shops.Skip(1)) o.Sim.ShareReputationWith(main);
            }
            if (_focus == s) SetFocus(MainShop);
            _vacant.Add(placeId);
            if (PropertyAt(placeId) == null) AddListing(City.GetPlace(placeId), Clock.Today.TotalDays, Clock.Today.TotalDays + 45, rent);
            WithShop(MainShop, ApplyUpgrades);
            Show(wasOwned ? "自营已结束" : "退租了", title + (wasOwned ? "已结束自营，产权保留为空置物业" : $"已退租，已交押金 {lostDeposit} 不退还，不会再次扣款")
                + "。店内货物和装修已清空" + (releasedStaff > 0 ? $"，{releasedStaff} 名员工转为待安排" : "")
                + (wasMain && MyShopCount > 0 ? $"。现在{ShopTitle(MainShop)}是主店。" : MyShopCount == 0 ? "。目前没有自营店，可到中介重新租铺。" : "。"));
            return true;
        }

        private void RedirectClosedShopDeliveries(OwnedShop shop)
        {
            foreach (var trip in _employeeTrips.Where(t => t.ShopId == shop.Place.Id))
            {
                trip.RemainingItems = null;
                if (!trip.Delivered) trip.DeliverHome = true;
            }
            foreach (var shipment in _directShipments.Where(s => DirectShipmentDestination(s) == shop)) shipment.DeliverHome = true;
            foreach (var order in Online.Pending.Where(o => OnlineOrderDestination(o) == shop)) order.ToHome = true;
            foreach (var parcel in _parcels.Where(p => ParcelDestination(p) == shop)) parcel.ToShop = false;
            foreach (var courier in _couriers.Where(r => r.ToShop && DeliveryShop(r.ShopId) == shop))
            {
                courier.ToShop = false;
                courier.ResidenceId = null;
            }
            _openRuns.Remove(RunKey(shop));
        }

        // ---------- 存档 ----------

        private SaveShop SaveShopOf(OwnedShop s)
        {
            ShopSimulation sim = s.Sim;
            return new SaveShop
            {
                retailKind = s.RetailKind, customName = s.CustomName, goodsLots = SaveRetailLots(sim),
                salesAccountId = s.SalesAccountId,
                emergencyLighting = s.EmergencyLighting,
                awareness = sim.Awareness, reputation = sim.ShopReputation,
                place = s.Place.Id,
                rentCents = s.Lease.Rent.Cents,
                depositCents = s.Lease.Deposit.Cents,
                startDay = s.Lease.StartDay,
                signedDay = s.Lease.SignedDay,
                stock = Lines(sim.Stock),
                goodsIds = SaveRetailCounts(sim).Select(kv => kv.Key).ToArray(),
                goodsCounts = SaveRetailCounts(sim).Select(kv => kv.Value).ToArray(),
                markup = sim.Markup,
                openMinute = sim.OpenMinute,
                closeMinute = sim.CloseMinute,
                orders = sim.Orders.Select(ToSave).ToArray(),
                nextOrderId = sim.NextOrderId,
                displayPcs = sim.DisplayPcs.ToArray(),
                partMarkupIds = sim.PartMarkups.Keys.ToArray(),
                partMarkupValues = sim.PartMarkups.Values.ToArray(),
                hasEmployee = s.Employee != null,
                employee = s.Employee != null ? ToSave(s.Employee) : null,
                hasAssembler = s.Assembler != null,
                assembler = s.Assembler != null ? ToSave(s.Assembler) : null,
                hasBuyer = s.Buyer != null,
                hasSalesperson = s.Salesperson != null,
                salesperson = s.Salesperson != null ? ToSave(s.Salesperson) : null,
                buyer = s.Buyer != null ? ToSave(s.Buyer) : null,
                autoBuild = s.AutoBuild,
                layout = ToSave(s.Layout),
                upgrades = s.Upgrades.ToArray(),
                restockIds = s.Restock.Keys.ToArray(),
                restockTargets = s.Restock.Values.ToArray(),
                renovationDay = s.RenovationDay,
                fridge = ToSaveStorage(s.Fridge),
                locker = ToSaveStorage(s.Locker),
                temp = ToSaveStorage(s.Temp),
                tempSince = s.TempSince,
                boxesGiven = s.BoxesGiven,
            };
        }

        private void RestoreShop(SaveShop d, bool hasShopStanding)
        {
            if (d == null || !City.HasPlace(d.place) || _shops.Any(x => x.Place.Id == d.place)) return;
            Place place = City.GetPlace(d.place);
            var shop = new OwnedShop { Place = place, RetailKind = RetailShopTypes.Valid(string.IsNullOrEmpty(d.retailKind) ? "computer" : d.retailKind), CustomName = d.customName };
            if (!string.IsNullOrEmpty(d.salesAccountId)) shop.SalesAccountId = d.salesAccountId;
            shop.EmergencyLighting = d.emergencyLighting;
            shop.Lease = new ShopLease { PlaceId = place.Id, Rent = Money.FromCents(d.rentCents), Deposit = Money.FromCents(d.depositCents), StartDay = d.startDay, SignedDay = d.signedDay };
            shop.Sim = CreateShopSim(shop);
            ShopSimulation sim = shop.Sim;
            sim.RestoreShopStanding(hasShopStanding ? d.awareness : MainShop.Sim.Reputation,
                hasShopStanding ? d.reputation : ShopSimulation.NeutralShopReputation);
            Fill(sim.Stock, d.stock);
            string[] goods = d.goodsIds ?? Array.Empty<string>();
            for (int i = 0; i < goods.Length && d.goodsCounts != null && i < d.goodsCounts.Length; i++) sim.Stock.ForceAddGoods(goods[i], d.goodsCounts[i]);
            sim.Stock.RestoreGoodsLots(d.goodsLots, Clock.Today.TotalDays);
            sim.Markup = d.markup;
            if (d.closeMinute > 0) sim.SetHours(d.openMinute, d.closeMinute);
            sim.RestoreOrders((d.orders ?? Array.Empty<SaveOrder>()).Select(FromSave), d.nextOrderId);
            sim.RestoreDisplay(d.displayPcs);
            string[] mids = d.partMarkupIds ?? Array.Empty<string>();
            double[] mvals = d.partMarkupValues ?? Array.Empty<double>();
            var marks = new List<KeyValuePair<string, double>>();
            for (int i = 0; i < mids.Length && i < mvals.Length; i++) marks.Add(new KeyValuePair<string, double>(mids[i], mvals[i]));
            sim.RestorePartMarkups(marks);
            shop.Employee = d.hasEmployee && d.employee != null ? FromSave(d.employee) : null;
            if (shop.Employee != null) sim.Settings.EmployeeCheckoutMinutes = EmployeeCheckoutCycleMinutes(shop.Employee) - 1;
            shop.Assembler = d.hasAssembler && d.assembler != null ? FromSave(d.assembler) : null;
            shop.Buyer = d.hasBuyer && d.buyer != null ? FromSave(d.buyer) : null;
            shop.Salesperson = d.hasSalesperson && d.salesperson != null ? FromSave(d.salesperson) : null;
            shop.AutoBuild = d.autoBuild;
            shop.Layout.AddRange(FromSave(d.layout));
            foreach (string id in d.upgrades ?? Array.Empty<string>())
                if (ShopUpgrades.Exists(id)) shop.Upgrades.Add(id);
            string[] rids = d.restockIds ?? Array.Empty<string>();
            int[] rt = d.restockTargets ?? Array.Empty<int>();
            for (int i = 0; i < rids.Length && i < rt.Length; i++)
                if (PartCatalog.Get(rids[i]) != null && rt[i] > 0) shop.Restock[rids[i]] = Math.Min(RestockMaxTarget, rt[i]);
            shop.RenovationDay = d.renovationDay;
            FromSaveStorage(d.fridge, shop.Fridge);
            FromSaveStorage(d.locker, shop.Locker);
            FromSaveStorage(d.temp, shop.Temp);
            shop.TempSince = d.tempSince;
            shop.BoxesGiven = d.boxesGiven;
            sim.ForcedClosed = shop.RenovationDay == Clock.Today.TotalDays;
            _shops.Add(shop);
            _vacant.Remove(place.Id);
            WithShop(shop, ApplyUpgrades);
        }

        private static SaveOrder ToSave(BuildOrder o) => new SaveOrder
        {
            id = o.Id, contractId = o.ContractId, companyBuildPackage = o.CompanyBuildPackage, tier = o.Tier, priceCents = o.Price.Cents, placedAt = o.PlacedAt.TotalMinutes,
            dueAt = o.DueAt.TotalMinutes, partsReserved = o.PartsReserved, mistakes = o.Mistakes, employeeMinutesLeft = o.EmployeeMinutesLeft,
            employeeAssemblyAptitude = o.EmployeeAssemblyAptitude,
            reservedPartMask = o.ReservedPartMask, boxedPartMask = o.BoxedPartMask,
        };

        private static BuildOrder FromSave(SaveOrder o) =>
            new BuildOrder(o.id, o.tier, Money.FromCents(o.priceCents), new GameTimestamp(o.placedAt), new GameTimestamp(o.dueAt))
            {
                ContractId = o.contractId, CompanyBuildPackage = o.companyBuildPackage, PartsReserved = o.partsReserved, Mistakes = Math.Max(0, o.mistakes), EmployeeMinutesLeft = Math.Max(0, o.employeeMinutesLeft),
                EmployeeAssemblyAptitude = Math.Max(0, Math.Min(100, o.employeeAssemblyAptitude)),
                ReservedPartMask = o.reservedPartMask, BoxedPartMask = o.boxedPartMask,
            };
    }
}
