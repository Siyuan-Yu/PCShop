using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Home;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Time;
using PCShopSell.Core.Trade;

namespace PCShopSell.Core.Session
{
    [Serializable] public sealed class ShopEventTask
    {
        public string Id, Kind, ShopId, RetailKind, PartId, Status, Before, Message;
        public int SignedDay, Remaining, OrderId, DecorationCount;
        public long At, Paid, LastMinute;
        public bool Started, Done;
        public ShopEventTask Copy() => (ShopEventTask)MemberwiseClone();
    }
    [Serializable] public sealed class ShopCatVisit
    {
        public string ShopId; public long Until; public bool Water;
        public ShopCatVisit Copy() => (ShopCatVisit)MemberwiseClone();
    }
    public sealed partial class GameSession
    {
        private readonly List<ShopEventTask> _shopEventTasks = new List<ShopEventTask>();
        private readonly HashSet<int> _eventDelayedOrders = new HashSet<int>();
        private ShopCatVisit _shopCat;
        private int _shopEventSequence;
        public ShopCatVisit EventCat => _shopCat != null && _shopCat.Until > Clock.Now.TotalMinutes && ShopAt(_shopCat.ShopId) != null ? _shopCat.Copy() : null;
        public ShopEventTask[] ShopEventTasks => _shopEventTasks.Where(t => !t.Done).Select(t => t.Copy()).ToArray();
        private OwnedShop ShopBatchShop() => MyShops.Where(s => !IsDedicatedVenue(s.Place) && !s.Sim.NotFitted)
            .OrderBy(s => s.Place == CurrentPlace ? 0 : 1).FirstOrDefault();
        private OwnedShop TaskShop(ShopEventTask t)
        {
            var shop = ShopAt(t.ShopId);
            return shop?.RetailKind == t.RetailKind && (shop.Lease?.SignedDay ?? -1) == t.SignedDay ? shop : null;
        }
        private ShopEventTask AddShopTask(string kind, OwnedShop shop, int minutes = 0)
        {
            var t = new ShopEventTask { Id = "shop-event:" + (++_shopEventSequence), Kind = kind, ShopId = shop.Place.Id,
                RetailKind = shop.RetailKind, SignedDay = shop.Lease?.SignedDay ?? -1, Remaining = minutes, Status = "待处理", LastMinute = Clock.Now.TotalMinutes };
            _shopEventTasks.Add(t); return t;
        }
        private bool HasShopTask(string kind, OwnedShop shop) => _shopEventTasks.Any(t => !t.Done && t.Kind == kind && t.ShopId == shop.Place.Id);
        internal bool CanOfferShopBatch(int number) => number switch {
            81 => Online.Pending.Any(o => !o.ToHome && !_eventDelayedOrders.Contains(o.Id) && o.TotalCents > 0),
            82 => EventPromotion() != null,
            83 => ShopBatchShop() is OwnedShop catShop && CurrentPlace == catShop.Place && EventCat == null,
            84 => IsMet("zhou") && MyShops.Any(s => s.RetailKind == "computer" && !s.Sim.NotFitted && !HasShopTask("sample", s)),
            85 => IsMet("jie") && CityPersonState("jie")?.WorkPlaceId != null && !_cityActivities.Any(a => a.Active && a.DefinitionId == "social_85"),
            86 => ComplaintReceipt() != null,
            87 => FindReferralShop(1) != null,
            88 => ShopBatchShop() is OwnedShop display && !HasShopTask("design", display) && !HasShopTask("self-display", display) && display.Layout.Any(p => IsDisplayDecoration(p.Id)),
            89 => ShopBatchShop() is OwnedShop audit && !HasShopTask("audit", audit),
            90 => CityAirTemperature >= 30 && ShopBatchShop() != null,
            _ => false };
        private static bool IsDisplayDecoration(string id) => id is "shop_plant" or "plant" or "shop_poster" or "poster";
        private EventDef BindShopBatch(EventDef template, int number)
        {
            if (!CanOfferShopBatch(number)) return null;
            if (number == 81) return BindDeliveryException(template);
            if (number == 82) return BindPriceWar(template);
            if (number == 84) return BindSupplierSample(template);
            if (number == 85) return BindSignHelp(template);
            if (number == 86) return BindReceiptComplaint(template);
            if (number == 87) return BindStudentOrderQuote(template);
            var shop = ShopBatchShop();
            if (number == 83)
            {
                _shopCat = new ShopCatVisit { ShopId = shop.Place.Id, Until = Clock.Now.TotalMinutes + 20 };
                return EventScene(template, "一只小猫来到" + ShopTitle(shop) + "门边，暂时趴着歇脚。",
                    Choose("放碗水", _ => { _shopCat.Water = true; _shopCat.Until = Clock.Now.TotalMinutes + 45; return "小猫喝了水，歇一会儿便会离开。"; }),
                    Choose("轻轻引到门外", _ => { _shopCat.Until = Clock.Now.TotalMinutes + 3; return "你把小猫引到门边，等它自己离开。"; }),
                    Choose("先不打扰", _ => "小猫暂时歇脚，没有成为店里的宠物。"));
            }
            if (number == 88)
                return EventScene(template, ShopTitle(shop) + "的陈列可以换个摆法。调整摆设沿用当天歇业装修规则。",
                    Choose("自己调整", _ => { var t = AddShopTask("self-display", shop); t.Before = LayoutStamp(shop); return "已记下当前陈列。到店从经营待办打开装修，实际调整后才算完成。"; }),
                    Choose("请设计师 · ¥1,500 / 30分钟", _ => { AddShopTask("design", shop, 30); return "已预约，到店从经营待办开始，届时歇业装修并支付¥1,500。"; }),
                    Choose("暂不改", _ => "保持当前陈列。"));
            if (number == 89)
                return EventScene(template, ShopTitle(shop) + "有库存变动待复核。盘点会核对实物、顾客预留和在途货，不随机扣款。",
                    Choose("现在盘点 · 30分钟", _ => { AddShopTask("audit", shop, 30); return "到店从经营待办开始；盘点期间暂停接待，完成后出核对结果。"; }),
                    Choose("打烊后盘点", _ => { var t = AddShopTask("audit", shop, 30); long end = Clock.Today.TotalDays * 1440L + shop.Sim.CloseMinute; t.At = end > Clock.Now.TotalMinutes ? end : end + 1440; return "已加入经营待办，打烊后到店开始。"; }));
            var choices = new List<EventChoice>();
            if (HasSeasonalAircon(shop.Place.Id)) choices.Add(Choose("已有空调，照常营业", _ => "空调已按现有规则改善室内舒适，电费沿用账单。"));
            else choices.Add(Choose("查看空调 · ¥1,200 / ¥3,600", _ => { Flags.Add("shop-event:aircon:" + shop.Place.Id); return "从经营待办进入家具采购，实际买入并安装后生效。"; }));
            bool eveningHours = Enumerable.Range(1080, 300).Any(m => shop.Sim.InHours(new GameTimestamp(Clock.Today.TotalDays * 1440L + m)));
            choices.Add(Choose("晚间宣传 · ¥1,000", s => {
                if (!eveningHours) return "本店没有晚间营业时段，未投放或扣款。";
                if (!s.Ledger.TryDebit(s.Clock.Today, "晚间宣传", Money.FromYuan(1000), ShopTitle(shop), LedgerAttribution.ForShop(shop.Place.Id))) return "现金不足，未投放。";
                var t = AddShopTask("evening", shop); t.Started = true; t.At = Clock.Today.TotalDays * 1440L + 18 * 60;
                if (t.At <= Clock.Now.TotalMinutes) t.At = Clock.Now.TotalMinutes + 1;
                return "已安排晚间宣传，18点后营业时带来潜在顾客，成交仍取决于实际商品。";
            }, s => eveningHours && s.Ledger.CanAfford(Money.FromYuan(1000)), "需要晚间营业时段和¥1,000预算"));
            choices.Add(Choose("照常营业", _ => "保持当前营业安排，不额外扣费。"));
            return EventScene(template, ShopTitle(shop) + "所在街区气温达到" + CityAirTemperature.ToString("0") + "℃。空调按实际设施生效，不另收一笔虚构电费。", choices.ToArray());
        }
        private EventDef BindDeliveryException(EventDef template)
        {
            var order = Online.Pending.First(o => !o.ToHome && !_eventDelayedOrders.Contains(o.Id) && o.TotalCents > 0);
            string Process(string mode)
            {
                if (!Online.Pending.Contains(order) || !_eventDelayedOrders.Add(order.Id)) return "订单已到货或已处理。";
                long at = (mode == "claim" ? Clock.Now.TotalMinutes : Math.Max(order.DeliverAtMinute, Clock.Now.TotalMinutes)) + (mode == "search" ? 180 : 2880);
                var record = _cityProcurementOrders.FirstOrDefault(r => r.OnlineOrderId == order.Id);
                if (mode == "claim")
                {
                    Online.CancelPending(order.Id);
                    foreach (var claim in _cityProcurementClaims.Where(c => c.OrderId == "online-order:" + order.Id))
                    { claim.State = ProcurementClaimState.Released; claim.Count = 0; claim.Reason = "已取消投递申请赔付"; }
                    var t = new ShopEventTask { Id = "delivery-claim:" + order.Id, Kind = "claim", OrderId = order.Id, At = at,
                        Paid = order.TotalCents, ShopId = order.ShopId, Started = true, Status = "赔付处理中" }; _shopEventTasks.Add(t);
                    if (record != null) { record.State = "赔付处理中"; record.DeliveryAt = at; }
                    return "已取消这笔货的投递，两天后按实付" + Money.FromCents(order.TotalCents) + "赔付，不再收到货物。";
                }
                order.DeliverAtMinute = at;
                if (record != null) { record.DeliveryAt = at; record.State = "查件中"; }
                return mode == "search" ? "已申请查件，原预计到货时间延后三小时，仍按原订单收货。" : "已等待物流处理，原预计到货时间延后两天。";
            }
            return EventScene(template, "网购订单#" + order.Id + "的包裹在转运中失联，实付" + Money.FromCents(order.TotalCents) + "。请选择查件或取消投递索赔。",
                Choose("等物流处理 · 延后两天", _ => Process("wait")), Choose("申请查件 · 延后三小时", _ => Process("search")), Choose("取消投递并索赔", _ => Process("claim")));
        }
        private EventDef BindSupplierSample(EventDef template)
        {
            var shop = MyShops.First(s => s.RetailKind == "computer" && !s.Sim.NotFitted && !HasShopTask("sample", s));
            var part = PartCatalog.Get("gpu1"); var price = Market.PriceOf(part);
            return EventScene(template, "周维成愿借一件" + part.Name + "给" + ShopTitle(shop) + "试用。次日10点送店，试完可归还或按" + price + "买下。",
                Choose("收一件试用样品", _ => { var t = AddShopTask("sample", shop); t.PartId = part.Id; t.Paid = price.Cents; t.At = GameTimestamp.From(Clock.Today.AddDays(1), 600).TotalMinutes; t.Status = "在途"; return "样品已安排送店，借用品单独记录，不能当库存卖掉。"; }),
                Choose("这次不需要", _ => "你婉拒了，不扣交情。"));
        }
        private EventDef BindSignHelp(EventDef template)
        {
            string venue = CityPersonState("jie").WorkPlaceId;
            long now = SuggestCityActivityStartAt("social_85", "jie", 0, venue);
            long next = SuggestCityActivityStartAt("social_85", "jie", Clock.Today.AddDays(1).TotalDays * 1440L + 540, venue);
            EventChoice Offer(string text, long start) => Choose(text + " · " + new GameTimestamp(start), s => {
                var a = s.ScheduleCityActivityAt("social_85", new[] { "jie" }, start, false, "sign-help:" + _nextCityActivity, venue);
                if (a == null) return "日程有变，未预约。";
                a.ServiceConfirmed = true; a.ServiceChoice = 0; return "已约好到店帮忙，实际一起忙完30分钟后增加交情。";
            }, _ => CityActivityBlockerAt("social_85", new[] { "jie" }, start, false, venue) == null, "暂无合适时段");
            return EventScene(template, "许文杰想换店门口的招牌，请你过去搭把手。", Offer("这次去帮忙", now), Offer("约明天帮忙", next), Choose("这次不方便", _ => "你婉拒了，不扣交情。"));
        }
        private EventPaidReceipt ComplaintReceipt() => _eventPaidReceipts.FirstOrDefault(r => !r.BadReview && r.Day >= Clock.Today.TotalDays - 7 && ReceiptShop(r) != null);
        private EventDef BindReceiptComplaint(EventDef template)
        {
            var receipt = ComplaintReceipt(); var shop = ReceiptShop(receipt);
            Money compensation = Money.FromCents(receipt.PaidCents).Scale(.1); if (compensation > Money.FromYuan(500)) compensation = Money.FromYuan(500);
            string Reply(string mode)
            {
                if (receipt.BadReview || ReceiptShop(receipt) != shop) return "这条评价已处理或门店已变化。";
                if (mode == "pay" && !Ledger.TryDebit(Clock.Today, "顾客补偿", compensation, receipt.Product, LedgerAttribution.ForShop(shop.Place.Id))) return "现金不足，尚未补偿。";
                if (mode == "pay") ReverseCompensatedMemberPoints(receipt, compensation.Cents);
                receipt.BadReview = true;
                var t = AddShopTask("complaint", shop); t.At = Clock.Now.TotalMinutes + 1440; t.Started = true;
                t.Message = mode == "pay" ? "补偿已到账，顾客收到处理说明；原评价是否修改由对方决定。" : mode == "ask" ? "已收到具体使用反馈，留作改进记录；没有直接删除评价。" : "已公开回复，说明售后处理方式；原评价保留。";
                return mode == "pay" ? "已支付" + compensation + "并提交处理说明，明天查看反馈。" : "已联系顾客，明天查看反馈。";
            }
            return EventScene(template, "一位购买过" + receipt.Product + "的顾客评价体验一般，实付" + Money.FromCents(receipt.PaidCents) + "。",
                Choose("了解具体问题", _ => Reply("ask")), Choose("协商补偿 · " + compensation, _ => Reply("pay"), s => s.Ledger.CanAfford(compensation), "现金不足"), Choose("公开回复", _ => Reply("reply")));
        }
        private EventDef BindStudentOrderQuote(EventDef template)
        {
            var shop = FindReferralShop(1); var choices = new List<EventChoice>(); string key = "student:" + Clock.Now.TotalMinutes + ":" + shop.Place.Id;
            void Offer(string label, int count, int basis)
            {
                var quote = QuoteIndustryReferral(shop, count); if (quote == null) return;
                quote.Id = key + ":" + count; quote.Client = "学生团购"; quote.CommunityOrder = true; quote.PriceBasisPoints = basis;
                choices.Add(Choose(label + " · " + quote.ProductName + " ×" + count + " · " + quote.Total, _ => {
                    if (ReferralShopFor(quote) != shop || _cityReferrals.Any(o => o.Id.StartsWith(key, StringComparison.Ordinal))) return "订单已处理或门店已变化。";
                    if (_cityReferrals.Any(o => o.ShopId == shop.Place.Id && o.Count == count && !o.Settled)) return "请先处理同类待交订单。";
                    _cityReferrals.Add(quote); return "已记录报价，请在门店订单确认接单并实际交货，当前没有收入。";
                }));
            }
            Offer("学生价九五折", 1, 9500); Offer("三件起九折", 3, 9000); Offer("按原价", 1, 10000);
            choices.Add(Choose("这次不接", _ => "本次没有订单或钱款变动。"));
            return EventScene(template, "几位学生向" + ShopTitle(shop) + "询价。按实际商品和数量报价，交付后才收款。", choices.ToArray());
        }
        private static string LayoutStamp(OwnedShop shop) => string.Join(";", shop.Layout.Where(p => IsDisplayDecoration(p.Id)).OrderBy(p => p.Id).ThenBy(p => p.X).ThenBy(p => p.Z).ThenBy(p => p.Rot)
            .Select(p => p.Id + ":" + p.X.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ":" + p.Z.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ":" + p.Rot));
        private string AuditStamp(OwnedShop shop)
        {
            int transit = Online.Pending.Where(o => !o.ToHome && OnlineOrderDestination(o) == shop).Sum(o => o.Lines.Sum(l => l.Count))
                + _directShipments.Where(d => d.ShopId == shop.Place.Id).Sum(d => d.Count)
                + _couriers.Where(c => c.ToShop && DeliveryShop(c.ShopId) == shop).Sum(c => c.Items.Sum(i => i.Count));
            var receipts = _eventPaidReceipts.Where(r => ReceiptShop(r) == shop && r.Day == Clock.Today.TotalDays).ToArray();
            string special = shop.RetailKind == "secondhand" ? "，二手实物" + SpecialBusiness(shop.Place.Id).Used.Count(i => i.Owned && !i.Sold) + "件"
                : shop.RetailKind == "restaurant" ? "，大米" + KitchenRice(shop).ToString("0.##") + "kg" : "";
            return "后仓配件" + shop.Sim.Stock.Lines().Sum(l => l.Count) + "件，零售商品" + shop.Sim.Stock.GoodsLines().Sum(l => l.Value) + "件" + special
                + "，待结账" + shop.Sim.Queue.Count + "位，在途" + transit + "件，今日成交" + receipts.Length + "笔/" + Money.FromCents(receipts.Sum(r => r.PaidCents));
        }
        public bool StartShopEventTask(string id)
        {
            var t = _shopEventTasks.FirstOrDefault(x => x.Id == id && !x.Done && !x.Started); if (t == null) return false;
            var shop = TaskShop(t);
            if (shop == null || CurrentPlace != shop.Place || Clock.Now.TotalMinutes < t.At) return Fail("请按约定时间到对应门店开始。");
            if (t.Kind == "sample")
            {
                if (t.Status != "已到店") return false;
                t.Remaining = 10; t.Started = true; t.LastMinute = Clock.Now.TotalMinutes; t.Status = "试用中"; return true;
            }
            if (t.Kind is not ("design" or "self-display" or "audit")) return false;
            bool ok = false;
            WithShop(shop, () => {
                if (t.Kind == "design" && !Ledger.CanAfford(Money.FromYuan(1500))) return;
                if (t.Kind is "design" or "self-display")
                { if (!BeginRenovation()) return; }
                if (t.Kind == "design" && !Ledger.TryDebit(Clock.Today, "陈列设计", Money.FromYuan(1500), ShopTitle(shop), LedgerAttribution.ForShop(shop.Place.Id))) return;
                t.Paid = t.Kind == "design" ? 150000 : 0;
                t.Before = t.Kind == "audit" ? AuditStamp(shop) : LayoutStamp(shop);
                t.DecorationCount = shop.Layout.Count(p => IsDisplayDecoration(p.Id));
                if (t.Kind == "audit") { t.Status = shop.Sim.ForcedClosed ? "原已歇业" : "原营业"; shop.Sim.ForcedClosed = true; }
                shop.Sim.ClearCustomers(); t.Started = true; t.LastMinute = Clock.Now.TotalMinutes; ok = true;
            });
            return ok;
        }
        public bool FinishSupplierSample(string id, bool buy)
        {
            var t = _shopEventTasks.FirstOrDefault(t => t.Id == id && t.Kind == "sample" && !t.Done && t.Status is "已到店" or "已试用");
            var shop = t == null ? null : TaskShop(t);
            if (shop == null || CurrentPlace != shop.Place) return false;
            var part = PartCatalog.Get(t.PartId);
            if (buy)
            {
                double incoming = 0; WithShop(shop, () => incoming = IncomingShopSpace);
                if (shop.Sim.Stock.FreeSpace - incoming < Stock.SpaceOf(part, true, 1)) return Fail("后仓空间不足，已计入在途货物。");
                if (!Ledger.TryDebit(Clock.Today, "样品买断", Money.FromCents(t.Paid), part.Name, LedgerAttribution.ForShop(shop.Place.Id))) return Fail("现金不足。");
                shop.Sim.Stock.ForceAdd(part, true, 1);
            }
            if (t.Status == "已试用") AddRelation("zhou", 2);
            t.Done = true; t.Status = buy ? "已买下" : "已归还"; Say("样品" + t.Status + "。"); return true;
        }
        private bool RearrangeForDesigner(OwnedShop shop)
        {
            bool moved = false;
            WithShop(shop, () => {
                for (int i = 0; i < shop.Layout.Count && !moved; i++)
                {
                    var p = shop.Layout[i]; if (!IsDisplayDecoration(p.Id)) continue;
                    foreach (var delta in new[] { (x: .5, z: 0.0), (x: -.5, z: 0.0), (x: 0.0, z: .5), (x: 0.0, z: -.5), (x: 1.0, z: 0.0), (x: -1.0, z: 0.0) })
                    {
                        var at = new PlacedFurniture { Id = p.Id, X = p.X + delta.x, Z = p.Z + delta.z, Rot = p.Rot };
                        if (PlacementError(true, FurnitureCatalog.Get(p.Id), at, i) == null && MoveFurniture(true, i, at)) { moved = true; break; }
                    }
                }
            });
            return moved;
        }
        private void TickShopEventBatch(long now)
        {
            TickFinanceBatch(now);
            foreach (var shop in MyShops.Where(s => HasSeasonalAircon(s.Place.Id))) Flags.Remove("shop-event:aircon:" + shop.Place.Id);
            if (_shopCat != null && now >= _shopCat.Until) _shopCat = null;
            foreach (var t in _shopEventTasks.Where(t => !t.Done).ToArray())
            {
                if (t.Kind is "bank" or "subsidy" or "subsidy-donation" or "return-customer") continue;
                if (t.Kind == "claim")
                {
                    if (now < t.At) continue;
                    Ledger.Credit(Clock.Today, "物流赔付", Money.FromCents(t.Paid), "网购订单#" + t.OrderId, LedgerAttribution.ForShop(t.ShopId));
                    var order = _cityProcurementOrders.FirstOrDefault(r => r.OnlineOrderId == t.OrderId); if (order != null) order.State = "已赔付";
                    t.Done = true; Say("物流赔付到账" + Money.FromCents(t.Paid) + "，该订单不再投递。"); continue;
                }
                var shop = TaskShop(t);
                if (shop == null)
                {
                    var changed = ShopAt(t.ShopId);
                    if (t.Kind == "audit" && t.Started && changed != null && (changed.Lease?.SignedDay ?? -1) == t.SignedDay)
                        changed.Sim.ForcedClosed = t.Status == "原已歇业" || changed.RenovationDay == Clock.Today.TotalDays;
                    if (t.Kind == "design" && t.Paid > 0) Ledger.Credit(Clock.Today, "设计退款", Money.FromCents(t.Paid), "原门店已停用");
                    t.Done = true; continue;
                }
                if (t.Kind == "sample" && t.Status == "在途" && now >= t.At) { t.Status = "已到店"; Say("试用样品已送到" + ShopTitle(shop) + "，可在经营待办试用、归还或买下。"); }
                if (t.Kind == "complaint" && now >= t.At) { t.Done = true; Say(t.Message); continue; }
                if (t.Kind == "evening" && now >= t.At && Clock.MinuteOfDay >= 1080 && shop.Sim.IsOpen)
                { _flyerCampaigns.Add(new FlyerCampaign { ShopId = shop.Place.Id, RetailKind = shop.RetailKind, StartDay = Clock.Today.TotalDays, Remaining = 4, NextVisit = now + 1 }); t.Done = true; continue; }
                if (!t.Started) continue;
                if (t.Kind == "audit") shop.Sim.ForcedClosed = true;
                long elapsed = Math.Max(0, Math.Min(1, now - t.LastMinute)); t.LastMinute = now;
                if (CurrentPlace != shop.Place || IsWorking || _sleeping) continue;
                if (t.Kind == "self-display")
                { if (LayoutStamp(shop) == t.Before || shop.Layout.Count(p => IsDisplayDecoration(p.Id)) < t.DecorationCount) continue; t.Done = true; AddDisplayVisitors(shop); continue; }
                if (t.Remaining <= 0) continue;
                t.Remaining -= (int)elapsed; if (t.Remaining > 0) continue;
                if (t.Kind == "sample") { t.Status = "已试用"; t.Started = false; Say("样品已试用，可归还或买下。"); continue; }
                if (t.Kind == "design")
                {
                    if (RearrangeForDesigner(shop)) { AddDisplayVisitors(shop); t.Message = "设计师已实际调整陈列。"; }
                    else { Ledger.Credit(Clock.Today, "设计退款", Money.FromCents(t.Paid), "没有可安全调整的位置"); t.Message = "没有可安全调整的位置，设计费已退回。"; }
                }
                if (t.Kind == "audit")
                {
                    t.Message = "盘点完成：" + AuditStamp(shop) + "。起始记录：" + t.Before + "。到货和顾客预留分别核对，未确认损耗，不额外扣钱。";
                    shop.Sim.ForcedClosed = t.Status == "原已歇业" || shop.RenovationDay == Clock.Today.TotalDays; Player.Intelligence = Math.Min(100, Player.Intelligence + 1);
                }
                t.Done = true; if (t.Message != null) Say(t.Message);
            }
        }
        private void AddDisplayVisitors(OwnedShop shop)
        {
            _flyerCampaigns.Add(new FlyerCampaign { ShopId = shop.Place.Id, RetailKind = shop.RetailKind, StartDay = Clock.Today.TotalDays + 1, Remaining = 6,
                NextVisit = Clock.Today.AddDays(1).TotalDays * 1440L + shop.Sim.OpenMinute }); Say("陈列已调整，未来营业时段会有额外顾客来看看，成交按实际商品处理。");
        }
        private void SaveShopEventBatch(SaveData d)
        { d.shopEventTasks = _shopEventTasks.Select(t => t.Copy()).ToArray(); d.eventDelayedOrders = _eventDelayedOrders.ToArray(); d.shopEventCat = _shopCat?.Copy(); d.shopEventSequence = _shopEventSequence; }
        private void RestoreShopEventBatch(SaveData d)
        { _shopEventTasks.Clear(); _shopEventTasks.AddRange((d.shopEventTasks ?? Array.Empty<ShopEventTask>()).Select(t => t.Copy())); _eventDelayedOrders.Clear(); foreach (int id in d.eventDelayedOrders ?? Array.Empty<int>()) _eventDelayedOrders.Add(id); _shopCat = d.shopEventCat?.Copy(); _shopEventSequence = d.shopEventSequence; }
    }
    public sealed partial class SaveData
    { public ShopEventTask[] shopEventTasks; public int[] eventDelayedOrders; public ShopCatVisit shopEventCat; public int shopEventSequence; }
}
