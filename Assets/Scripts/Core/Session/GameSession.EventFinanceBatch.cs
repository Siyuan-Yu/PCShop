using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Shop;
using PCShopSell.Core.Time;
using PCShopSell.Core.Trade;

namespace PCShopSell.Core.Session
{
    [Serializable] public sealed class EventMember
    {
        public string ShopId, PersonId; public int SignedDay, Points; public long NetPaid;
        public EventMember Copy() => (EventMember)MemberwiseClone();
    }
    [Serializable] public sealed class EventFlashSale
    {
        public string ShopId, RetailKind, ProductId; public int SignedDay, Remaining; public long Until;
        public EventFlashSale Copy() => (EventFlashSale)MemberwiseClone();
    }
    [Serializable] public sealed class EventPurchaseCoupon
    {
        public string SupplierId; public long Until; public bool Used;
        public EventPurchaseCoupon Copy() => (EventPurchaseCoupon)MemberwiseClone();
    }
    [Serializable] public sealed class EventFlashReservation
    {
        public string ShopId, ProductId; public int CustomerId; public long Until;
        public EventFlashReservation Copy() => (EventFlashReservation)MemberwiseClone();
    }
    public sealed partial class GameSession
    {
        private readonly List<EventMember> _eventMembers = new List<EventMember>();
        private readonly List<EventFlashSale> _eventFlashSales = new List<EventFlashSale>();
        private readonly List<EventFlashReservation> _eventFlashReservations = new List<EventFlashReservation>();
        private EventPurchaseCoupon _eventCoupon;
        public EventMember[] EventMembers => _eventMembers.Select(m => m.Copy()).ToArray();
        public EventFlashSale[] EventFlashSales => _eventFlashSales.Where(f => FlashScheduled(f)).Select(f => f.Copy()).ToArray();
        public EventPurchaseCoupon EventCoupon => _eventCoupon?.Copy();
        private string MemberFlag(OwnedShop s) => "event-members:" + s.Place.Id + ":" + (s.Lease?.SignedDay ?? -1);
        public bool HasEventMembership(OwnedShop shop) => shop != null && Flags.Contains(MemberFlag(shop));
        private string SubsidyFlag(OwnedShop s) => "event-subsidy:" + s.Place.Id + ":" + (s.Lease?.SignedDay ?? -1) + ":" + Clock.Today.TotalDays / GameDate.DaysPerYear;
        private bool SubsidyEligible(OwnedShop s) => s != null && Clock.Today.TotalDays - (s.Lease?.SignedDay ?? Clock.Today.TotalDays) >= 30
            && !Flags.Contains(SubsidyFlag(s)) && _eventPaidReceipts.Any(r => ReceiptShop(r) == s);
        private EventPaidReceipt ReturnReceipt() => _eventPaidReceipts.LastOrDefault(r => !r.ReturnInvited && !string.IsNullOrEmpty(r.PersonId)
            && ReceiptShop(r)?.Sim.IsOpen == true && EventShelfProducts(ReceiptShop(r)).Any() && AvailableReturnPerson(r.PersonId));
        private bool AvailableReturnPerson(string id)
        {
            var p = CityPersonState(id);
            return p != null && p.Route == null && p.ActivityId == null && p.VisitId == null && p.Task != CityPersonTask.Working && p.Task != CityPersonTask.Waiting
                && !Employees.Any(e => e.Person.PersonId == id) && !_candidates.Any(e => e.PersonId == id)
                && !_cityActivities.Any(a => a.Active && a.Participants.Any(who => who.PersonId == id));
        }
        internal bool CanOfferFinanceBatch(int n) => n switch {
            91 => ReturnReceipt() != null,
            92 => HasSmartphone && EventRetailShops().Any(s => EventShelfProducts(s).Any() && !_eventFlashSales.Any(f => f.ShopId == s.Place.Id && FlashScheduled(f))),
            93 => IsMet("res_rider") && ShopBatchShop() != null && !_cityActivities.Any(a => a.Active && a.DefinitionId == "social_93"),
            94 => _inspectionFollowups.Keys.Any(id => ShopAt(id) != null),
            95 => ShopBatchShop()?.RetailKind == "computer" && HasSmartphone,
            96 => EventRetailShops().Any(s => !HasEventMembership(s)),
            97 => HasSmartphone && (_eventCoupon == null || _eventCoupon.Used || Clock.Now.TotalMinutes >= _eventCoupon.Until),
            98 => MyShops.Any(SubsidyEligible),
            99 => HasSmartphone,
            100 => BankPlace != null && BankDeposit >= Money.FromYuan(1000) && !_shopEventTasks.Any(t => t.Kind == "bank" && !t.Done),
            _ => false };
        private EventDef BindFinanceBatch(EventDef template, int n)
        {
            if (!CanOfferFinanceBatch(n)) return null;
            if (n == 91)
            {
                var r = ReturnReceipt(); var shop = ReceiptShop(r); var product = EventShelfProducts(shop).First();
                string Invite(bool recommend)
                {
                    if (r.ReturnInvited || ReceiptShop(r) != shop || !AvailableReturnPerson(r.PersonId)) return "顾客或门店状态已变化，未安排到访。";
                    r.ReturnInvited = true;
                    var t = AddShopTask("return-customer", shop); t.PartId = recommend ? product.Id : null; t.Message = r.PersonId; t.Paid = Math.Max(r.PaidCents, EventPrice(shop, product).Cents); t.At = Clock.Now.TotalMinutes + 15; t.Started = true;
                    CityPersonState(r.PersonId).VisitId = t.Id;
                    return "已邀请这位老顾客回店，按实际到店、挑选和结账处理。";
                }
                return EventScene(template, (CityPerson(r.PersonId)?.Name ?? r.PersonId) + "曾买过" + r.Product + "，想再来看看。",
                    Choose("推荐现货 · " + product.Name, _ => Invite(true)), Choose("请他到店慢慢选", _ => Invite(false)), Choose("下次再来", _ => { r.ReturnInvited = true; return "这次没有安排到店。"; }));
            }
            if (n == 92) return BindFlashSale(template);
            if (n == 93)
            {
                var shop = ShopBatchShop();
                string Invite(bool water)
                {
                    long at = SuggestCityActivityStartAt("social_93", "res_rider", 0, shop.Place.Id);
                    var a = ScheduleCityActivityAt("social_93", new[] { "res_rider" }, at, false, "rider-rest:" + Clock.Now.TotalMinutes, shop.Place.Id);
                    if (a == null) return "骑手当前无法前来，未安排。";
                    a.ServiceConfirmed = true; a.ServiceChoice = 0; return water ? "已约骑手来店门边歇脚喝水，实际相处20分钟后增加交情。" : "已约骑手来歇脚20分钟；没有充电或额外收入。";
                }
                return EventScene(template, "骑手想在" + ShopTitle(shop) + "门边歇脚20分钟。", Choose("让他歇会儿", _ => Invite(false)), Choose("招呼他喝水休息", _ => Invite(true)), Choose("这次不方便", _ => "你婉拒了，不扣交情。"));
            }
            if (n == 94)
            {
                var check = _inspectionFollowups.First(x => ShopAt(x.Key) != null); var shop = ShopAt(check.Key);
                bool fitted = IsSpecialBusiness(shop) ? SpecialFitGap(shop) == null : !shop.Sim.NotFitted;
                return EventScene(template, ShopTitle(shop) + "此前有整改记录，将于" + new GameDate(check.Value) + "复查。按实际设施和进货单据核验。",
                    Choose("查看待整改项", _ => "设施：" + (fitted ? "已配齐" : "尚未配齐") + "；进货单据：" + (shop.Sim.Stock.IsEmpty || HasPurchaseReceipt(shop) ? "无需补充" : "待补充") + "。原复查时间不变。"),
                    Choose("按原定日期复查", _ => "沿用原复查记录，不重复结算。"));
            }
            if (n == 95)
            {
                var shop = ShopBatchShop(); var part = PartCatalog.Get("gpu1"); var price = OnlinePrice(Online1788.Shopping, part);
                string Buy(int count) => PlaceOnlineOrder(Online1788.Shopping.Id, new[] { (part, count) }, shop) ? "已按实时采购价下单，次日到货；涨价传闻不保证收益。" : "未下单，请检查现金、容量和采购条件。";
                return EventScene(template, "听说" + part.Name + "可能涨价。当前网购单价" + price + "，另计实际运费；传闻没有保本承诺。",
                    Choose("少量试单 · 1件", _ => Buy(1)), Choose("备货 · 3件", _ => Buy(3)), Choose("自选采购数量", _ => { Flags.Add("shop-event:procure:" + shop.Place.Id); return "已加入经营待办，打开采购后自行选数量并确认付款。"; }), Choose("先不囤货", _ => "未采购，也没有额外收益。"));
            }
            if (n == 96)
            {
                var shop = EventRetailShops().First(s => !HasEventMembership(s));
                return EventScene(template, ShopTitle(shop) + "可开通会员积分：会员实付累计满¥100积1分，50分抵后续一单¥100。不开储值。",
                    Choose("开通会员 · ¥600", _ => {
                        if (HasEventMembership(shop) || ShopAt(shop.Place.Id) != shop) return "门店状态已变化或已开通。";
                        if (!Ledger.TryDebit(Clock.Today, "会员开通", Money.FromYuan(600), ShopTitle(shop), LedgerAttribution.ForShop(shop.Place.Id))) return "现金不足，未开通。";
                        Flags.Add(MemberFlag(shop)); return "已开通，结账时登记实名会员积分，后续消费达到兑换条件自动抵扣。";
                    }), Choose("暂不开通", _ => "保持原结账方式。"));
            }
            if (n == 97)
                return EventScene(template, Online1788.Shopping.Name + "发来采购券：商品满¥5,000减¥500，14天内限用一次，不含运费。",
                    Choose("领取采购券", _ => { _eventCoupon = new EventPurchaseCoupon { SupplierId = Online1788.Shopping.Id, Until = Clock.Now.TotalMinutes + 14 * 1440L }; return "已领取，符合条件的下一笔网购采购自动使用；取消订单按实付退款，券不退。"; }),
                    Choose("不领取", _ => "未领取，没有现金收入。"));
            if (n == 98)
            {
                var shop = MyShops.First(SubsidyEligible); string flag = SubsidyFlag(shop);
                return EventScene(template, ShopTitle(shop) + "开店已满30天且有实际成交，可申请本年度¥3,000经营补贴，审核3天。",
                    Choose("申请经营补贴", _ => { if (!SubsidyEligible(shop)) return "本年度已申请或条件已变化。"; Flags.Add(flag); var t = AddShopTask("subsidy", shop); t.Started = true; t.At = Clock.Now.TotalMinutes + 3 * 1440L; t.Status = "审核中"; return "已申请，三天后按门店状态审核到账。"; }),
                    Choose("暂不申请", _ => "未提交申请。"));
            }
            if (n == 99)
                return EventScene(template, "电话里的人自称银行工作人员，让你转¥5,000到所谓安全账户。这是诈骗。",
                    Choose("挂断电话", _ => "你挂断了，没有财产损失。"),
                    Choose("去银行核实", _ => { var t = new ShopEventTask { Id = "bank-check:" + (++_shopEventSequence), Kind = "bank", ShopId = BankPlaceId, Status = "到柜台核实", Remaining = 5 }; _shopEventTasks.Add(t); return "已记入经营待办，请到银行柜台核实。"; }),
                    Choose("仍转账 · 损失¥5,000", _ => Ledger.TryDebit(Clock.Today, "诈骗损失", Money.FromYuan(5000), "向虚假安全账户转账") ? "已实际损失¥5,000，不保证追回。" : "现金不足，未转账。"));
            string nextDue = AppLoans.Where(l => !l.Repaid).OrderBy(l => l.DueDay).Select(l => new GameDate(l.DueDay).ToString()).FirstOrDefault() ?? "无待还借款";
            return EventScene(template, "银行客户经理邀请你了解柜台业务。存款" + BankDeposit + "，借款待还" + AppLoanDebt + "，最近到期：" + nextDue + "。存款月息0.3%。",
                Choose("预约柜台咨询 · 30分钟", _ => { _shopEventTasks.Add(new ShopEventTask { Id = "bank-advice:" + (++_shopEventSequence), Kind = "bank", ShopId = BankPlaceId, Remaining = 30, Status = "待到柜台咨询" }); return "已加入经营待办，到银行营业时开始，不自动存款或借款。"; }),
                Choose("只看业务说明", _ => "存取款和借还款请在银行柜台自行选择，不自动购买理财。"), Choose("暂不需要", _ => "没有资金或属性变化。"));
        }
        private EventDef BindFlashSale(EventDef template)
        {
            var shop = EventRetailShops().First(s => EventShelfProducts(s).Any() && !_eventFlashSales.Any(f => f.ShopId == s.Place.Id && FlashScheduled(f)));
            var choices = new List<EventChoice>();
            foreach (var item in EventShelfProducts(shop))
            {
                var product = item;
                foreach (bool paid in new[] { false, true })
                    choices.Add(Choose(product.Name + (paid ? " · 推广¥1,000" : " · 免费分享"), _ => {
                        if (ShopAt(shop.Place.Id) != shop || !EventShelfProducts(shop).Any(p => p.Id == product.Id) || _eventFlashSales.Any(f => f.ShopId == shop.Place.Id && FlashScheduled(f))) return "商品已售罄或促销已开始。";
                        if (paid && !Ledger.TryDebit(Clock.Today, "限时推广", Money.FromYuan(1000), product.Name, LedgerAttribution.ForShop(shop.Place.Id))) return "现金不足，未投放。";
                        _eventFlashSales.Add(new EventFlashSale { ShopId = shop.Place.Id, RetailKind = shop.RetailKind, ProductId = product.Id, SignedDay = shop.Lease?.SignedDay ?? -1, Until = Clock.Now.TotalMinutes + 120, Remaining = 5 });
                        _flyerCampaigns.Add(new FlyerCampaign { ShopId = shop.Place.Id, RetailKind = shop.RetailKind, StartDay = Clock.Today.TotalDays, Remaining = paid ? 8 : 2, NextVisit = Clock.Now.TotalMinutes + 1 });
                        return "该商品九折2小时、最多5笔成交，到期恢复价格；推广带来潜在顾客，不保证成交。";
                    }));
            }
            choices.Add(Choose("暂不促销", _ => "未调整价格。"));
            return EventScene(template, ShopTitle(shop) + "：选一种在售商品，九折2小时、限5笔成交。", choices.ToArray());
        }
        private bool FlashScheduled(EventFlashSale f) => Clock.Now.TotalMinutes < f.Until && ShopAt(f.ShopId) is OwnedShop s && s.RetailKind == f.RetailKind && (s.Lease?.SignedDay ?? -1) == f.SignedDay;
        private bool ValidFlash(EventFlashSale f) => f.Remaining > 0 && FlashScheduled(f);
        private double FinanceFlashFactor(OwnedShop shop, string productId) => _eventFlashSales.Any(f => ValidFlash(f) && f.ShopId == shop.Place.Id && f.ProductId == productId) ? .9 : 1;
        private EventMember MemberFor(OwnedShop shop, string personId)
        {
            if (!HasEventMembership(shop) || string.IsNullOrEmpty(personId)) return null;
            var member = _eventMembers.FirstOrDefault(m => m.ShopId == shop.Place.Id && m.SignedDay == (shop.Lease?.SignedDay ?? -1) && m.PersonId == personId);
            if (member == null) { member = new EventMember { ShopId = shop.Place.Id, SignedDay = shop.Lease?.SignedDay ?? -1, PersonId = personId }; _eventMembers.Add(member); }
            return member;
        }
        private Money MemberSalePrice(OwnedShop shop, ShopCustomer c)
        {
            var member = MemberFor(shop, c.PersonId);
            if (member == null || member.Points < 50 || c.Spend < Money.FromYuan(100)) return c.Spend;
            member.Points -= 50; return c.Spend - Money.FromYuan(100);
        }
        private void RecordFinanceSale(OwnedShop shop, ShopCustomer c)
        {
            var member = MemberFor(shop, c.PersonId);
            if (member != null) { long before = member.NetPaid / 10000; member.NetPaid += c.Spend.Cents; member.Points += (int)(member.NetPaid / 10000 - before); }
            _eventFlashReservations.RemoveAll(r => r.ShopId == shop.Place.Id && r.CustomerId == c.Id);
        }
        private void ReserveFlashPrice(OwnedShop shop, ShopCustomer c)
        {
            string id = c.GoodsId ?? c.Item?.Id;
            var flash = _eventFlashSales.FirstOrDefault(f => ValidFlash(f) && f.ShopId == shop.Place.Id && f.ProductId == id);
            if (flash == null || _eventFlashReservations.Any(r => r.ShopId == shop.Place.Id && r.CustomerId == c.Id)) return;
            flash.Remaining--;
            _eventFlashReservations.Add(new EventFlashReservation { ShopId = shop.Place.Id, ProductId = id, CustomerId = c.Id, Until = flash.Until });
        }
        private void ReleaseFlashPrice(OwnedShop shop, ShopCustomer c)
        {
            var r = _eventFlashReservations.FirstOrDefault(x => x.ShopId == shop.Place.Id && x.CustomerId == c.Id);
            if (r == null) return;
            var flash = _eventFlashSales.FirstOrDefault(f => f.ShopId == r.ShopId && f.ProductId == r.ProductId && f.Until == r.Until);
            if (flash != null && Clock.Now.TotalMinutes < flash.Until) flash.Remaining++;
            _eventFlashReservations.Remove(r);
        }
        private void ReverseCompensatedMemberPoints(EventPaidReceipt receipt, long cents)
        {
            var member = _eventMembers.FirstOrDefault(m => m.ShopId == receipt.ShopId && m.SignedDay == receipt.SignedDay && m.PersonId == receipt.PersonId);
            if (member == null) return;
            long before = member.NetPaid / 10000; member.NetPaid = Math.Max(0, member.NetPaid - cents); member.Points -= (int)(before - member.NetPaid / 10000);
        }
        private Money ProcurementCouponDiscount(OnlineOrder order, Money total)
        {
            var c = _eventCoupon;
            return c != null && !c.Used && c.SupplierId == order.StoreId && Clock.Now.TotalMinutes < c.Until && !order.ToHome && total - Online1788.ShippingFee >= Money.FromYuan(5000) ? Money.FromYuan(500) : Money.Zero;
        }
        public Money EventProcurementCouponQuote(Money goods, bool toHome) => ProcurementCouponDiscount(new OnlineOrder { StoreId = Online1788.Shopping.Id, ToHome = toHome }, goods + Online1788.ShippingFee);
        public bool StartBankEventTask(string id)
        {
            var t = _shopEventTasks.FirstOrDefault(x => x.Id == id && x.Kind == "bank" && !x.Done && !x.Started);
            if (t == null || BankBlocker() != null) return false;
            t.Started = true; t.LastMinute = Clock.Now.TotalMinutes; t.Status = "柜台咨询中"; return true;
        }
        public bool DonateEventSubsidy(string id)
        {
            var t = _shopEventTasks.FirstOrDefault(x => x.Id == id && x.Kind == "subsidy-donation" && !x.Done);
            if (t == null || !Ledger.TryDebit(Clock.Today, "公益捐赠", Money.FromYuan(1500), "经营补贴自愿捐出一半")) return false;
            t.Done = true; return true;
        }
        private void TickFinanceBatch(long now)
        {
            _eventFlashSales.RemoveAll(f => Clock.Now.TotalMinutes >= f.Until || ShopAt(f.ShopId) is not OwnedShop s || s.RetailKind != f.RetailKind || (s.Lease?.SignedDay ?? -1) != f.SignedDay);
            foreach (var t in _shopEventTasks.Where(t => !t.Done && t.Kind is "bank" or "subsidy" or "return-customer").ToArray())
            {
                if (t.Kind == "bank")
                {
                    long delta = Math.Max(0, Math.Min(1, now - t.LastMinute)); t.LastMinute = now;
                    if (!t.Started || BankBlocker() != null || _sleeping) continue;
                    t.Remaining -= (int)delta;
                    if (t.Remaining <= 0) { t.Done = true; if (t.Id.StartsWith("bank-advice:") && Flags.Add("event-bank-advice-complete")) Player.Intelligence = Math.Min(100, Player.Intelligence + 1); Say("柜台核实完成。存款" + BankDeposit + "；存取款与借还款请自行选择。没有自动转账。"); }
                    continue;
                }
                var shop = TaskShop(t); if (shop == null) { ReleaseReturnInvitation(t); t.Done = true; continue; }
                if (now < t.At) continue;
                if (t.Kind == "subsidy")
                {
                    Ledger.Credit(Clock.Today, "经营补贴", Money.FromYuan(3000), ShopTitle(shop), LedgerAttribution.ForShop(shop.Place.Id)); t.Done = true;
                    var donation = AddShopTask("subsidy-donation", shop); donation.Status = "补贴已到账，可自愿捐出¥1,500";
                    Say("经营补贴¥3,000到账，捐赠可在经营待办自愿选择。"); continue;
                }
                if (now > t.At + 1440) { ReleaseReturnInvitation(t); t.Done = true; continue; }
                if (!shop.Sim.IsOpen) continue;
                var reservedPerson = CityPersonState(t.Message);
                if (reservedPerson?.VisitId != t.Id) { t.Done = true; continue; }
                var person = CityPersonState(t.Message); if (PlanCityRoute(person, shop.Place.Id, now) == null) continue;
                if (t.PartId == null) shop.Sim.ArriveReturningCustomer(t.Message);
                else { var item = EventShelfProducts(shop).FirstOrDefault(p => p.Id == t.PartId); if (item == null) { ReleaseReturnInvitation(t); t.Done = true; continue; } shop.Sim.ArriveReferral(item.Part, item.Part == null ? item.Id : null, item.Boxed, t.Paid, false, t.Message); }
                t.Done = true;
            }
        }
        private void ReleaseReturnInvitation(ShopEventTask t)
        {
            if (t.Kind != "return-customer") return;
            var p = CityPersonState(t.Message);
            if (p?.VisitId == t.Id) { p.VisitId = null; p.Task = CityPersonTask.Returning; }
        }
        private void SaveFinanceBatch(SaveData d)
        { d.eventMembers = EventMembers; d.eventFlashSales = _eventFlashSales.Select(f => f.Copy()).ToArray(); d.eventFlashReservations = _eventFlashReservations.Select(r => r.Copy()).ToArray(); d.eventCoupon = _eventCoupon?.Copy(); }
        private void RestoreFinanceBatch(SaveData d)
        {
            _eventMembers.Clear(); _eventMembers.AddRange((d.eventMembers ?? Array.Empty<EventMember>()).Select(m => m.Copy()));
            _eventFlashSales.Clear(); _eventFlashSales.AddRange((d.eventFlashSales ?? Array.Empty<EventFlashSale>()).Select(f => f.Copy()));
            _eventFlashReservations.Clear(); _eventFlashReservations.AddRange((d.eventFlashReservations ?? Array.Empty<EventFlashReservation>()).Select(r => r.Copy()));
            _eventCoupon = d.eventCoupon?.Copy();
            // City visit restoration clears claims without an active browsing customer.
            // Reinstate pending invitations only after normal customers have been restored.
            foreach (var t in _shopEventTasks.Where(t => t.Kind == "return-customer" && !t.Done))
            {
                var p = CityPersonState(t.Message);
                if (p != null && p.VisitId == null && TaskShop(t) != null && Clock.Now.TotalMinutes <= t.At + 1440)
                { p.VisitId = t.Id; p.Task = CityPersonTask.Shopping; }
                else t.Done = true;
            }
        }
    }
    public sealed partial class SaveData { public EventMember[] eventMembers; public EventFlashSale[] eventFlashSales; public EventFlashReservation[] eventFlashReservations; public EventPurchaseCoupon eventCoupon; }
}
