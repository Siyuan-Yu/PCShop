using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Trade;

namespace PCShopSell.Core.Session
{
    public enum ProcurementMethod { Online1788, Employee, DirectSupply }
    public enum ProcurementClaimState { Claimed, Ordered, Released }
    [Serializable]
    public sealed class ProcurementPolicy
    {
        public string ShopId, ProductId, EmployeeId;
        public PurchaseGoodsKind Kind;
        public ProcurementMethod Primary;
        public bool AllowFallback;
        public bool ExplicitChoice;
        public int Target, Lower = -1;
        public ProcurementPolicy Copy() => (ProcurementPolicy)MemberwiseClone();
    }
    [Serializable]
    public sealed class ProcurementClaim
    {
        public string Id, ShopId, ProductId, OwnerId, SourceId, OrderId, Reason;
        public PurchaseGoodsKind Kind;
        public ProcurementMethod Method;
        public ProcurementClaimState State;
        public int Count;
        public long BudgetCents, CreatedAt;
        public ProcurementClaim Copy() => (ProcurementClaim)MemberwiseClone();
    }
    public sealed class ProcurementCoverage
    {
        public int FreeStock, InTransit, Claimed, Target, Lower, ContractMissing;
        public int Effective => FreeStock + InTransit + Claimed;
        public int Uncovered => (Effective <= Lower ? Math.Max(0, Target - Effective) : 0) + ContractMissing;
    }
    public sealed class ProcurementSourceQuote
    {
        public string OfferId, SupplierId, SupplierName, ProductId, Name, PlaceId, Blocker, Conditions;
        public PurchaseGoodsKind Kind;
        public ProcurementMethod Method;
        public Money UnitPrice, Shipping;
        public int Minimum = 1, LeadMinutes;
    }
    [Serializable]
    public sealed class ProcurementOrderRecord
    {
        public string Id, ShopId, SupplierId, Channel, EmployeeId, State;
        public int OnlineOrderId;
        public long PaidCents, PlacedAt, DeliveryAt;
        public PurchaseLine[] Lines;
        public ProcurementOrderRecord Copy() { var c = (ProcurementOrderRecord)MemberwiseClone(); c.Lines = (Lines ?? Array.Empty<PurchaseLine>()).Select(l => l.Copy()).ToArray(); return c; }
    }
    public sealed partial class GameSession
    {
        private readonly Dictionary<string, ProcurementPolicy> _cityProcurementPolicies = new Dictionary<string, ProcurementPolicy>();
        private readonly List<ProcurementClaim> _cityProcurementClaims = new List<ProcurementClaim>();
        private int _cityProcurementSequence = 1;
        private readonly List<ProcurementOrderRecord> _cityProcurementOrders = new List<ProcurementOrderRecord>();
        public IReadOnlyList<ProcurementOrderRecord> ProcurementOrders => _cityProcurementOrders;
        public IReadOnlyList<ProcurementClaim> ProcurementClaims => _cityProcurementClaims;
        private static string DemandKey(string shop, PurchaseGoodsKind kind, string product) => shop + "|" + (int)kind + "|" + product;

        /// <summary>Reading a default policy never saves, starts a purchase or changes the focused shop.</summary>
        public ProcurementPolicy ProcurementPolicyFor(string shopId, PurchaseGoodsKind kind, string productId)
        {
            if (_cityProcurementPolicies.TryGetValue(DemandKey(shopId, kind, productId), out var saved)) return saved.Copy();
            var shop = ShopAt(shopId);
            int target = kind == PurchaseGoodsKind.Part && shop != null && shop.Restock.TryGetValue(productId, out int t) ? t : 0;
            var buyer = shop?.Buyer;
            bool assigned = buyer?.PurchasePlan != null && buyer.PurchasePlan.Daily && buyer.PurchasePlan.Items.Any(i => i.Kind == kind && i.PartId == productId);
            return new ProcurementPolicy { ShopId = shopId, Kind = kind, ProductId = productId, Target = target,
                Primary = assigned || kind != PurchaseGoodsKind.Part ? ProcurementMethod.Employee : ProcurementMethod.Online1788,
                EmployeeId = assigned ? buyer.Id : null };
        }

        public bool SetProcurementPolicy(ProcurementPolicy policy)
        {
            var shop = ShopAt(policy?.ShopId);
            if (shop == null || string.IsNullOrEmpty(policy.ProductId)) return Fail("请选择有效门店和商品。");
            var p = policy.Copy(); p.ExplicitChoice = true; p.Target = Math.Max(0, Math.Min(p.Kind == PurchaseGoodsKind.Part ? RestockMaxTarget : 100, p.Target)); p.Lower = Math.Max(0, Math.Min(p.Target, p.Lower < 0 ? (int)(p.Target * RestockTriggerRatio) : p.Lower));
            if (p.Primary != ProcurementMethod.Online1788 && p.Primary != ProcurementMethod.Employee) return Fail("自动补货请选择1788或指定进货员，直供由玩家确认。");
            if (p.Kind != PurchaseGoodsKind.Part && p.Primary == ProcurementMethod.Online1788) return Fail("1788 经营批发目前供应电脑配件；这件商品请选择进货员。");
            if (p.Primary == ProcurementMethod.Employee)
            {
                var worker = FindEmployee(p.EmployeeId);
                if (worker?.Shop != shop || worker.Role != EmployeeRole.Buyer) return Fail("请选择本店的进货员。");
            }
            _cityProcurementPolicies[DemandKey(p.ShopId, p.Kind, p.ProductId)] = p;
            if (p.Kind == PurchaseGoodsKind.Part) { if (p.Target == 0) shop.Restock.Remove(p.ProductId); else shop.Restock[p.ProductId] = p.Target; }
            return true;
        }
        public string ProcurementExecutorStatus(ProcurementPolicy policy)
        {
            if(policy.Primary==ProcurementMethod.Online1788)return AppBlocker(PhoneApps.Wholesale)??"1788按目标自动补货";
            var worker=FindEmployee(policy.EmployeeId);
            if(worker?.Shop?.Place.Id!=policy.ShopId||worker.Role!=EmployeeRole.Buyer)return "原采购员已换岗/离职；缺口保留，不自动换渠道";
            if(worker.Person.PurchasePlan?.Daily!=true)return "进货员定期计划尚未启用或已暂停";
            if(worker.Person.PurchasePlan.FirstDay>Clock.Today.TotalDays)return "计划从第"+(worker.Person.PurchasePlan.FirstDay+1)+"天生效";
            return CityEmployeeAvailable(worker.Person,worker.Shop)?"进货员按班次与周期执行":"进货员休息/未到岗/被活动占用，等待可执行时段";
        }
        private void BindDefaultProcurementOwner(EmployeeAssignment worker,EmployeePurchasePlan plan)
        {
            foreach(var item in plan.Items)
            {
                string key=DemandKey(worker.Shop.Place.Id,item.Kind,item.PartId);
                var policy=ProcurementPolicyFor(worker.Shop.Place.Id,item.Kind,item.PartId);
                if(policy.Target<=0||policy.ExplicitChoice)continue;
                policy.Primary=ProcurementMethod.Employee;policy.EmployeeId=worker.Person.Id;
                _cityProcurementPolicies[key]=policy;
            }
        }

        public int ClaimedProcurementCount(string shopId, PurchaseGoodsKind kind, string productId, string excludingOwner = null) =>
            _cityProcurementClaims.Where(c => c.State == ProcurementClaimState.Claimed && c.ShopId == shopId && c.Kind == kind && c.ProductId == productId
                && (excludingOwner == null || c.OwnerId != excludingOwner)).Sum(c => c.Count);
        public Money ClaimedProcurementBudget => ClaimedProcurementBudgetExcept(null);
        private Money ClaimedProcurementBudgetExcept(string ownerId) => Money.FromCents(_cityProcurementClaims.Where(c => c.State == ProcurementClaimState.Claimed && (ownerId == null || c.OwnerId != ownerId)).Sum(c => c.BudgetCents));
        private double ClaimedProcurementSpace(string shopId) => _cityProcurementClaims.Where(c => c.State == ProcurementClaimState.Claimed && c.ShopId == shopId).Sum(c =>
            c.Kind == PurchaseGoodsKind.Part && PartCatalog.Exists(c.ProductId) ? Stock.SpaceOf(PartCatalog.Get(c.ProductId), true, c.Count)
            : c.Kind == PurchaseGoodsKind.Gift && Gifts.Get(c.ProductId) != null ? Stock.GoodsSpace(c.ProductId, c.Count) : 0);

        public ProcurementCoverage ProcurementCoverageFor(string shopId, PurchaseGoodsKind kind, string productId, string excludingOwner = null)
        {
            var shop = ShopAt(shopId); var policy = ProcurementPolicyFor(shopId, kind, productId);
            int stock = shop == null ? 0 : kind == PurchaseGoodsKind.Part && PartCatalog.Exists(productId) ? shop.Sim.Stock.CountAll(PartCatalog.Get(productId))
                : kind == PurchaseGoodsKind.Gift ? shop.Sim.Stock.GoodsCount(productId) : 0;
            // Materials for already reserved builds have left Stock. For other committed orders,
            // allocate remaining stock and supply to their deadline order before retail targets.
            int required = kind == PurchaseGoodsKind.Part && shop != null ? shop.Sim.Orders.Where(o => !o.PartsReserved)
                .Count(o => PartCatalog.BuildList(o.Tier).Any(p => p.Id == productId)) : 0;
            int inTransit = PendingProcurementCount(shop, kind, productId), claims = ClaimedProcurementCount(shopId, kind, productId, excludingOwner);
            int awaiting = Math.Max(0, required - stock), retailTransit = Math.Max(0, inTransit - awaiting);
            return new ProcurementCoverage { FreeStock = Math.Max(0, stock - required), InTransit = retailTransit,
                Claimed = Math.Max(0, inTransit + claims - awaiting) - retailTransit, ContractMissing = Math.Max(0, awaiting - inTransit - claims), Target = policy.Target,
                Lower = policy.Lower < 0 ? (int)(policy.Target * RestockTriggerRatio) : policy.Lower };
        }

        public IReadOnlyList<PartDef> UncoveredBuildProcurement(OwnedShop shop, PCShopSell.Core.Shop.BuildOrder order)
        {
            if (shop == null || order == null || order.PartsReserved || !shop.Sim.Orders.Contains(order)) return Array.Empty<PartDef>();
            return PartCatalog.BuildList(order.Tier).Where(part => UncoveredBuildPartCount(shop, order, part.Id) > 0).ToArray();
        }
        public int UncoveredBuildPartCount(OwnedShop shop, PCShopSell.Core.Shop.BuildOrder order, string partId)
        {
            if (shop == null || order == null || order.PartsReserved || !PartCatalog.Exists(partId) || !shop.Sim.Orders.Contains(order)) return 0;
            int required = shop.Sim.Orders.Where(o => !o.PartsReserved && (o.DueAt < order.DueAt || (o.DueAt == order.DueAt && o.Id <= order.Id)))
                .Count(o => PartCatalog.BuildList(o.Tier).Any(p => p.Id == partId));
            return Math.Max(0, required - shop.Sim.Stock.CountAll(PartCatalog.Get(partId)) - PendingProcurementCount(shop, PurchaseGoodsKind.Part, partId)
                - ClaimedProcurementCount(shop.Place.Id, PurchaseGoodsKind.Part, partId));
        }

        public int PendingProcurementCount(OwnedShop shop, PurchaseGoodsKind kind, string productId)
        {
            if (shop == null) return 0;
            int count = kind == PurchaseGoodsKind.Part ? Online.Pending.Where(o => !o.ToHome && DeliveryShop(o.ShopId) == shop).Sum(o => o.Lines.Where(l => l.PartId == productId).Sum(l => l.Count))
                + DirectShipments.Where(o => o.PartId == productId && DeliveryShop(o.ShopId) == shop).Sum(o => o.Count) : 0;
            count += _employeeTrips.Where(t => !t.Delivered && DeliveryShop(t.ShopId) == shop).Sum(t => t.Items.Where(i => i.Kind == kind && i.PartId == productId).Sum(i => i.Count));
            foreach (var route in Couriers.Where(r => r.ToShop && DeliveryShop(r.ShopId) == shop))
                count += route.Items.Where(i => i.Id == productId && ((kind == PurchaseGoodsKind.Part && i.Kind == CourierItemKind.Part) || (kind == PurchaseGoodsKind.Gift && i.Kind == CourierItemKind.Goods))).Sum(i => i.Count);
            return count;
        }

        public string ClaimProcurementDemand(string shopId, PurchaseGoodsKind kind, string productId, int count, ProcurementMethod method, string ownerId, string sourceId)
        {
            var shop = ShopAt(shopId); if (shop == null || count <= 0 || count > 100 || string.IsNullOrEmpty(ownerId) || method == ProcurementMethod.DirectSupply) return null;
            var quote = ProcurementSources(kind, productId).FirstOrDefault(o => o.Method == method && o.SupplierId == sourceId && o.Blocker == null);
            if (quote == null || ProcurementCoverageFor(shopId, kind, productId).Uncovered < count) return null;
            Money budget = quote.UnitPrice * count + quote.Shipping;
            if (Ledger.Balance < EffectiveRestockReserve + ClaimedProcurementBudget + budget) return null;
            double room = 0; WithShop(shop, () => room = shop.Sim.Stock.FreeSpace - IncomingShopSpace);
            if (PurchaseStockSpace(new PurchaseLine { Kind = kind, PartId = productId, Count = count }) > room + 1e-9) return null;
            string id = "demand:" + _cityProcurementSequence++;
            _cityProcurementClaims.Add(new ProcurementClaim { Id = id, ShopId = shopId, Kind = kind, ProductId = productId, Count = count,
                Method = method, OwnerId = ownerId, SourceId = sourceId, BudgetCents = budget.Cents, CreatedAt = Clock.Now.TotalMinutes });
            return id;
        }
        public bool CancelProcurementClaim(string id)
        {
            var claim = _cityProcurementClaims.FirstOrDefault(c => c.Id == id && c.State == ProcurementClaimState.Claimed);
            if (claim == null) return false; claim.State = ProcurementClaimState.Released; claim.BudgetCents = 0; claim.Reason = "已取消，缺口与预算已释放"; return true;
        }

        private bool PayUnifiedOnlineOrder(OnlineOrder order, Money total, OnlineStore store)
        {
            if (!Ledger.CanAfford(total)) return false;
            string orderId = "online-order:" + order.Id;
            foreach (var line in order.Lines)
            {
                var unit = OnlinePrice(store, PartCatalog.Get(line.PartId));
                var attribution = order.ToHome ? new LedgerAttribution(LedgerBusinessKind.Personal, sourceEventId: orderId + ":" + line.PartId, transactionKind: "个人采购")
                    : LedgerAttribution.ForShop(order.ShopId, orderId + ":" + line.PartId, "采购").Product(line.PartId, line.Count, unit.Cents);
                if (!Ledger.TryDebit(Clock.Today, "进货", unit * line.Count, "1788 " + store.Name, attribution))
                    throw new InvalidOperationException("1788总额预检通过后扣款失败，未继续发货。");
            }
            if (Online1788.ShippingFee.IsPositive) Ledger.ForceDebit(Clock.Today, "运费", Online1788.ShippingFee, "1788 " + store.Name,
                order.ToHome ? new LedgerAttribution(LedgerBusinessKind.Personal, sourceEventId: orderId + ":shipping", transactionKind: "个人采购运费") : LedgerAttribution.ForShop(order.ShopId, orderId + ":shipping", "物流"));
            return true;
        }

        private void RegisterProcurementOrder(string id, string channel, string shopId, string supplier, long paid, long delivery,
            IEnumerable<PurchaseLine> lines, int onlineOrder = 0, string employee = null)
        {
            if (_cityProcurementOrders.Any(o => o.Id == id)) return;
            _cityProcurementOrders.Add(new ProcurementOrderRecord { Id = id, Channel = channel, ShopId = shopId, SupplierId = supplier, PaidCents = paid,
                PlacedAt = Clock.Now.TotalMinutes, DeliveryAt = delivery, Lines = lines.Select(l => l.Copy()).ToArray(), OnlineOrderId = onlineOrder, EmployeeId = employee, State = "在途" });
        }

        public bool CancelOnlineProcurementOrder(int id)
        {
            var pending = Online.Pending.FirstOrDefault(o => o.Id == id);
            if (pending == null || Clock.Now.TotalMinutes >= pending.DeliverAtMinute || IsGameOver) return Fail("订单已交付或不可取消。");
            var order = Online.CancelPending(id); if (order == null) return false;
            Ledger.Credit(Clock.Today, "进货退款", Money.FromCents(order.TotalCents), "1788取消订单 #" + id,
                order.ToHome ? new LedgerAttribution(LedgerBusinessKind.Personal, sourceEventId: "online-order:" + id + ":refund", transactionKind: "个人采购退款") : LedgerAttribution.ForShop(order.ShopId, "online-order:" + id + ":refund", "采购退款"));
            var record = _cityProcurementOrders.FirstOrDefault(o => o.Id == "online-order:" + id);
            if (record != null) record.State = "已取消并退款";
            foreach (var claim in _cityProcurementClaims.Where(c => c.OrderId == "online-order:" + id))
            { claim.State = ProcurementClaimState.Released; claim.Count = 0; claim.Reason = "订单取消，缺口已释放"; }
            Say("订单已取消，货款与运费退回，仓位和缺口已释放。"); return true;
        }

        public bool ExecuteProcurementClaim(string id)
        {
            var claim = _cityProcurementClaims.FirstOrDefault(c => c.Id == id && c.State == ProcurementClaimState.Claimed);
            if (claim == null) return false;
            if (claim.Method == ProcurementMethod.Employee) return Fail("进货员认领按保存的班次/采购周期执行，不立即扣款出发。");
            var shop = ShopAt(claim.ShopId); if (shop == null || claim.Kind != PurchaseGoodsKind.Part || !PartCatalog.Exists(claim.ProductId)) return false;
            Money actual = OnlinePrice(Online1788.GetStore(claim.SourceId), PartCatalog.Get(claim.ProductId)) * claim.Count + Online1788.ShippingFee;
            if (Ledger.Balance < actual + EffectiveRestockReserve + ClaimedProcurementBudget - Money.FromCents(claim.BudgetCents)) return Fail("当前报价超过可用采购资金，保留认领等待处理。");
            claim.State = ProcurementClaimState.Ordered; claim.BudgetCents = 0;
            int onlineId = Online.NextOrderId; bool success = false;
            WithShop(shop, () => success = PlaceOnlineOrderCore(claim.SourceId, new[] { (PartCatalog.Get(claim.ProductId), claim.Count) }, false));
            if (success) claim.OrderId = "online-order:" + onlineId;
            else { claim.State = ProcurementClaimState.Claimed; claim.BudgetCents = actual.Cents; }
            return success;
        }

        /// <summary>Called by the unified world-minute tick, not from UI getters.</summary>
        private void UpdateCityProcurement()
        {
            foreach (var record in _cityProcurementOrders.Where(o => o.State == "在途" || o.State == "已送达，员工返程中"))
            {
                if (record.OnlineOrderId > 0) { if (!Online.Pending.Any(o => o.Id == record.OnlineOrderId)) record.State = "已交付"; }
                else if (record.EmployeeId != null)
                {
                    var trip = _employeeTrips.FirstOrDefault(t => t.OrderId == record.Id);
                    record.State = trip == null ? "已交付" : trip.Delivered ? "已送达，员工返程中" : "在途";
                }
                else if (record.Channel == "直供" && !_directShipments.Any(s => s.OrderId == record.Id)) record.State = "已交付";
            }
            foreach (var claim in _cityProcurementClaims.Where(c => c.State == ProcurementClaimState.Claimed))
                if (ShopAt(claim.ShopId) == null || (claim.Method == ProcurementMethod.Employee && FindEmployee(claim.OwnerId)?.Role != EmployeeRole.Buyer))
                { claim.State = ProcurementClaimState.Released; claim.BudgetCents = 0; claim.Reason = "执行者/门店不再有效，认领已释放"; }
        }
        private void HandOffProcurementClaims(string ownerId, string shopId, IEnumerable<PurchaseLine> lines, string orderId)
        {
            foreach (var claim in _cityProcurementClaims.Where(c => c.State == ProcurementClaimState.Claimed && c.OwnerId == ownerId && c.ShopId == shopId))
            {
                int fulfilled = lines.Where(l => l.Kind == claim.Kind && l.PartId == claim.ProductId).Sum(l => l.Count);
                claim.Count = Math.Min(claim.Count, fulfilled); claim.State = claim.Count > 0 ? ProcurementClaimState.Ordered : ProcurementClaimState.Released;
                claim.OrderId = orderId; claim.BudgetCents = 0; claim.Reason = claim.Count > 0 ? "执行已转为在途，只计一次覆盖" : "本次未购，认领已释放";
            }
        }

        public IReadOnlyList<ProcurementSourceQuote> ProcurementSources(PurchaseGoodsKind kind, string productId)
        {
            var result = new List<ProcurementSourceQuote>();
            foreach (var offer in EmployeePurchaseCatalog().Where(o => o.Kind == kind && o.Id == productId))
                result.Add(new ProcurementSourceQuote { OfferId = offer.Key, SupplierId = offer.Source.Id, SupplierName = offer.Source.Name, ProductId = productId,
                    Kind = kind, Name = offer.Name, PlaceId = offer.Source.Id, UnitPrice = offer.Price, Method = ProcurementMethod.Employee,
                    Minimum = CitySupplierMinimum(offer.Source.Id),
                    Conditions = "线下看货/议价；员工按真实路线与运费执行 · 起订 " + CitySupplierMinimum(offer.Source.Id) + " 件"
                        + (kind == PurchaseGoodsKind.Gift && CitySupplierRemaining(offer.Source.Id, productId) < int.MaxValue ? " · 本日剩余配额 " + CitySupplierRemaining(offer.Source.Id, productId) : ""), LeadMinutes = 0 });
            if (kind == PurchaseGoodsKind.Part && PartCatalog.Exists(productId))
            {
                foreach (var store in Online1788.Stores.Where(st => Online.IsDiscovered(st.Id)))
                    result.Add(new ProcurementSourceQuote { OfferId = "1788:" + store.Id + ":" + productId, SupplierId = store.Id, SupplierName = store.Name,
                        Kind = kind, ProductId = productId, Name = PartCatalog.Get(productId).Name, UnitPrice = OnlinePrice(store, PartCatalog.Get(productId)), Shipping = Online1788.ShippingFee,
                        Method = ProcurementMethod.Online1788, Blocker = AppBlocker(PhoneApps.Wholesale), LeadMinutes = (24 * 60 - Clock.MinuteOfDay) + Online1788.DeliveryMinuteOfDay,
                        Conditions = "常规经营批发；次日10:00配送，同订单只收一次运费" });
                if (DirectAgreement)
                    result.Add(new ProcurementSourceQuote { OfferId = "direct:" + productId, SupplierId = "direct:zhou", SupplierName = "老周协议直供",
                        ProductId = productId, Kind = kind, Name = PartCatalog.Get(productId).Name, UnitPrice = DirectPrice(PartCatalog.Get(productId)), Minimum = 3,
                        Method = ProcurementMethod.DirectSupply, LeadMinutes = 1440 - Clock.MinuteOfDay + 600, Conditions = "协议货源；每型3–30件，次日10:00配送；保留原直供授信入口" });
            }
            return result.OrderBy(o => o.UnitPrice.Cents).ToArray();
        }

        public bool QueueUnifiedEmployeePurchase(string employeeId, PurchaseGoodsKind kind, string productId, string sourceId, int count)
        {
            var row = FindEmployee(employeeId); if (row?.Shop == null || row.Role != EmployeeRole.Buyer) return false;
            var plan = row.Person.PurchasePlan?.Copy() ?? new EmployeePurchasePlan(); PrepareEmployeePurchaseDraft(plan);
            var offer = EmployeePurchaseOffer(new PurchaseLine { Kind = kind, PartId = productId, SourceId = sourceId, Count = count });
            if (offer == null || count <= 0 || count > 100) return Fail("该来源没有此商品，或数量超出范围。");
            var items = plan.Items.ToList(); var existing = items.FirstOrDefault(i => i.Kind == kind && i.PartId == productId && i.SourceId == sourceId);
            if (existing == null) items.Add(offer.Line(count)); else existing.Count = Math.Max(existing.Count, count);
            plan.Items = items.ToArray(); return CompleteEmployeePurchase(employeeId, plan);
        }

        public bool PlaceUnifiedOnlinePurchase(string shopId, string storeId, string partId, int count)
        {
            var shop = ShopAt(shopId);
            if (shop == null || !PartCatalog.Exists(partId) || count < 1 || count > 100) return Fail("采购目的店或商品已不可用。");
            bool success = false;
            WithShop(shop, () => success = PlaceOnlineOrderCore(storeId, new[] { (PartCatalog.Get(partId), count) }, true));
            return success;
        }
        public bool PlaceUnifiedDirectPurchase(string shopId, string partId, int count)
        {
            var shop = ShopAt(shopId); if (shop == null) return Fail("门店已不可用。");
            bool result = false; WithShop(shop, () => result = OrderDirect(partId, count, false)); return result;
        }

        private EmployeePurchasePlan EffectiveEmployeePurchasePlan(EmployeeAssignment row, EmployeePurchasePlan source)
        {
            if (source == null || !source.InventoryThreshold) return source;
            var plan = source.Copy(); plan.InventoryThreshold = false; var lines = new List<PurchaseLine>();
            var policies = _cityProcurementPolicies.Values.Where(p => p.ShopId == row.Shop.Place.Id).Select(p => p.Copy()).ToList();
            foreach (var entry in row.Shop.Restock)
                if (!policies.Any(p => p.Kind == PurchaseGoodsKind.Part && p.ProductId == entry.Key)) policies.Add(ProcurementPolicyFor(row.Shop.Place.Id, PurchaseGoodsKind.Part, entry.Key));
            foreach (var policy in policies.Where(p => p.Primary == ProcurementMethod.Employee && (p.EmployeeId == null || p.EmployeeId == row.Person.Id) && p.Target > 0))
            {
                int need = ProcurementCoverageFor(row.Shop.Place.Id, policy.Kind, policy.ProductId, row.Person.Id).Uncovered;
                if (need <= 0) continue;
                var preference = source.Items.FirstOrDefault(i => i.Kind == policy.Kind && i.PartId == policy.ProductId);
                var offer = EmployeePurchaseOffer(preference ?? new PurchaseLine { Kind = policy.Kind, PartId = policy.ProductId });
                if (offer == null) continue;
                int count = Math.Min(100, need);
                if (policy.Kind == PurchaseGoodsKind.Gift && Gifts.Get(policy.ProductId)?.ShelfDays > 0) count = Math.Min(count, Math.Max(1, Gifts.Get(policy.ProductId).ShelfDays * 2));
                lines.Add(offer.Line(count));
            }
            plan.Items = lines.ToArray(); return plan;
        }

        private EmployeePurchasePlan AutomaticEmployeePurchasePlan(EmployeeAssignment row)
        {
            var source = row.Person.PurchasePlan;
            if (source == null || source.InventoryThreshold) return source;
            var plan = source.Copy();
            plan.Items = plan.Items.Where(item =>
            {
                var policy = ProcurementPolicyFor(row.Shop.Place.Id, item.Kind, item.PartId);
                if (policy.Target <= 0) return true; // An unrelated fixed shopping list keeps its intent.
                if (policy.Primary != ProcurementMethod.Employee || (policy.EmployeeId != null && policy.EmployeeId != row.Person.Id)) return false;
                var c = ProcurementCoverageFor(row.Shop.Place.Id, item.Kind, item.PartId, row.Person.Id);
                return c.Uncovered > 0 || (c.InTransit + c.Claimed == 0);
            }).ToArray();
            return plan;
        }

        private void SaveCityProcurement(SaveData d)
        {
            d.cityProcurementVersion = 1; d.cityProcurementNextId = _cityProcurementSequence;
            d.cityProcurementPolicies = _cityProcurementPolicies.Values.Select(p => p.Copy()).ToArray();
            d.cityProcurementClaims = _cityProcurementClaims.Select(c => c.Copy()).ToArray();
            d.cityProcurementOrders = _cityProcurementOrders.Skip(Math.Max(0, _cityProcurementOrders.Count - 300)).Select(o => o.Copy()).ToArray();
        }
        private void LoadCityProcurement(SaveData d)
        {
            _cityProcurementPolicies.Clear(); _cityProcurementClaims.Clear(); _cityProcurementSequence = Math.Max(1, d.cityProcurementNextId);
            _cityProcurementOrders.Clear();
            foreach (var o in d.cityProcurementOrders ?? Array.Empty<ProcurementOrderRecord>())
                if (o != null && !string.IsNullOrEmpty(o.Id) && !_cityProcurementOrders.Any(x => x.Id == o.Id)) _cityProcurementOrders.Add(o.Copy());
            foreach (var o in Online.Pending)
                RegisterProcurementOrder("online-order:" + o.Id, "1788", o.ShopId, o.StoreId, o.TotalCents, o.DeliverAtMinute,
                    o.Lines.Select(l => new PurchaseLine { Kind = PurchaseGoodsKind.Part, PartId = l.PartId, Count = l.Count }), o.Id);
            foreach (var trip in _employeeTrips)
            {
                if (string.IsNullOrEmpty(trip.OrderId)) trip.OrderId = "legacy-employee:" + trip.EmployeeId + ":" + trip.BoughtAt;
                RegisterProcurementOrder(trip.OrderId, "员工代采（旧档在途）", trip.ShopId, null, 0, trip.DeliveryAt, trip.Items ?? Array.Empty<PurchaseLine>(), 0, trip.EmployeeId);
            }
            int directIndex = 0;
            foreach (var direct in _directShipments)
            {
                if (string.IsNullOrEmpty(direct.OrderId)) direct.OrderId = "legacy-direct:" + direct.ShopId + ":" + direct.Arrival + ":" + directIndex++;
                RegisterProcurementOrder(direct.OrderId, "直供", direct.ShopId, "direct:zhou", 0, direct.Arrival,
                    new[] { new PurchaseLine { Kind = PurchaseGoodsKind.Part, PartId = direct.PartId, Count = direct.Count, SourceId = "direct:zhou" } });
            }
            foreach (var p in d.cityProcurementPolicies ?? Array.Empty<ProcurementPolicy>())
                if (p != null && ShopAt(p.ShopId) != null && !string.IsNullOrEmpty(p.ProductId)) _cityProcurementPolicies[DemandKey(p.ShopId, p.Kind, p.ProductId)] = p.Copy();
            foreach(var shop in MyShops)
                foreach(var target in shop.Restock)
                {
                    string key=DemandKey(shop.Place.Id,PurchaseGoodsKind.Part,target.Key);
                    if(!_cityProcurementPolicies.ContainsKey(key))_cityProcurementPolicies[key]=ProcurementPolicyFor(shop.Place.Id,PurchaseGoodsKind.Part,target.Key);
                }
            foreach (var c in d.cityProcurementClaims ?? Array.Empty<ProcurementClaim>())
            {
                if (c == null || string.IsNullOrEmpty(c.Id) || _cityProcurementClaims.Any(x => x.Id == c.Id)) continue;
                var copy = c.Copy();
                copy.Count = Math.Max(0, Math.Min(100, copy.Count)); copy.BudgetCents = Math.Max(0, copy.BudgetCents);
                if (ShopAt(copy.ShopId) == null || (copy.Method == ProcurementMethod.Employee && FindEmployee(copy.OwnerId) == null))
                { copy.State = ProcurementClaimState.Released; copy.BudgetCents = 0; copy.Reason = "原执行者或门店已失效，释放认领"; }
                _cityProcurementClaims.Add(copy);
            }
        }
    }
}
