using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Shop;

namespace PCShopSell.Core.Session
{
    [Serializable] public sealed class EventPaidReceipt
    {
        public string Key, ShopId, RetailKind, Product, PersonId;
        public int SignedDay, Day, CustomerId, ChangeDay;
        public long PaidCents, ChangeCents, RefundAt;
        public bool ReturnInvited;
        public bool Cash, Reviewed, ChangeRecorded, Refunded, ComplaintApplied, BadReview;
        public EventPaidReceipt Copy() => (EventPaidReceipt)MemberwiseClone();
    }
    public sealed partial class GameSession
    {
        private readonly List<EventPaidReceipt> _eventPaidReceipts = new List<EventPaidReceipt>();
        public IReadOnlyList<EventPaidReceipt> PendingChangeRefunds => _eventPaidReceipts.Where(r => r.ChangeRecorded && !r.Refunded).Select(r => r.Copy()).ToArray();
        public Money ChangeRefundDebt => Money.FromCents(_eventPaidReceipts.Where(r => r.ChangeRecorded && !r.Refunded).Sum(r => r.ChangeCents));
        private void RecordAggregateReceipt(OwnedShop shop, string saleId, string product, Money paid, int day)
        {
            string key = shop.Place.Id + ":" + (shop.Lease?.SignedDay ?? -1) + ":" + shop.RetailKind + ":" + day + ":" + saleId;
            if (_eventPaidReceipts.Any(r => r.Key == key)) return;
            _eventPaidReceipts.Add(new EventPaidReceipt { Key = key, ShopId = shop.Place.Id, RetailKind = shop.RetailKind,
                SignedDay = shop.Lease?.SignedDay ?? -1, Day = day, Product = product, PaidCents = paid.Cents });
        }
        private void RecordEventPaidReceipt(OwnedShop shop, ShopCustomer customer)
        {
            string key = shop.Place.Id + ":" + (shop.Lease?.SignedDay ?? -1) + ":" + shop.RetailKind + ":" + Clock.Today.TotalDays + ":" + customer.Id;
            if (customer.Spend.Cents <= 0 || _eventPaidReceipts.Any(r => r.Key == key)) return;
            _eventPaidReceipts.RemoveAll(r => r.Day < Clock.Today.TotalDays - 8 && (!r.ChangeRecorded || r.Refunded));
            _eventPaidReceipts.Add(new EventPaidReceipt { Key = key, ShopId = shop.Place.Id, RetailKind = shop.RetailKind,
                SignedDay = shop.Lease?.SignedDay ?? -1, Day = Clock.Today.TotalDays, CustomerId = customer.Id, PersonId = customer.PersonId, PaidCents = customer.Spend.Cents,
                Product = customer.ItemName, Cash = customer.PaidWith == PayChannel.Cash });
        }
        private OwnedShop ReceiptShop(EventPaidReceipt receipt)
        {
            var shop = receipt == null ? null : ShopAt(receipt.ShopId);
            return shop != null && shop.RetailKind == receipt.RetailKind && (shop.Lease?.SignedDay ?? -1) == receipt.SignedDay ? shop : null;
        }
        internal bool CanOfferCustomerReview => _eventPaidReceipts.Any(r => !r.Reviewed && r.Day >= Clock.Today.TotalDays - 7 && ReceiptShop(r) != null);
        internal bool CanOfferChangeDiscrepancy => _eventPaidReceipts.Any(r => r.Cash && !r.Refunded && ReceiptShop(r) != null
            && (r.ChangeRecorded || r.Day >= Clock.Today.TotalDays - 1));
        private EventDef BindCustomerReview(EventDef template)
        {
            var r = _eventPaidReceipts.LastOrDefault(r => !r.Reviewed && r.Day >= Clock.Today.TotalDays - 7 && ReceiptShop(r) != null);
            if (r == null) return null; var shop = ReceiptShop(r);
            string Reply(int mode)
            {
                if (r.Reviewed || ReceiptShop(r) != shop) return "这条顾客记录已经处理或门店已变化。";
                r.Reviewed = true; shop.Sim.AdjustShopReputation(mode == 2 ? .5 : 1);
                if (mode == 2)
                    _flyerCampaigns.Add(new FlyerCampaign { ShopId = shop.Place.Id, RetailKind = shop.RetailKind,
                        StartDay = Clock.Today.TotalDays + 1, Remaining = 2, NextVisit = (Clock.Today.TotalDays + 1) * 1440L });
                return mode == 2 ? "已征得这位顾客同意转发，明天起有两位潜在顾客在营业时到店；是否成交仍看真实库存和服务。"
                    : mode == 1 ? "你问了具体使用感受，反馈记在这家门店，口碑 +1。" : "你感谢了这位真实成交顾客，门店口碑 +1。";
            }
            return EventScene(template, $"一位在{ShopTitle(shop)}买过{r.Product}、实付 ¥{r.PaidCents / 100.0:0.00} 的顾客发布了店铺推荐。",
                Choose("发消息感谢", _ => Reply(0)), Choose("询问具体使用意见", _ => Reply(1)),
                Choose("征求同意后转发", _ => Reply(2)), Choose("暂不回复", _ => { r.Reviewed = true; return "本次没有回复，不发奖励。"; }));
        }
        private EventDef BindWrongChange(EventDef template)
        {
            var r = _eventPaidReceipts.FirstOrDefault(r => r.Cash && r.ChangeRecorded && !r.Refunded && ReceiptShop(r) != null)
                ?? _eventPaidReceipts.LastOrDefault(r => r.Cash && !r.Refunded && r.Day >= Clock.Today.TotalDays - 1 && ReceiptShop(r) != null);
            if (r == null) return null; var shop = ReceiptShop(r);
            if (!r.ChangeRecorded)
            {
                r.ChangeRecorded = true; r.ChangeDay = Clock.Today.TotalDays; r.ChangeCents = Math.Min(5000, Math.Max(100, r.PaidCents / 10));
                Ledger.Credit(Clock.Today, "收银待退差额", Money.FromCents(r.ChangeCents), "盘点确认现金少找 · " + r.Product,
                    LedgerAttribution.ForShop(shop.Place.Id, r.Key + ":change", "待退款"));
            }
            return EventScene(template, $"复核{ShopTitle(shop)}的一笔真实现金交易：{r.Product}，商品价款 ¥{r.PaidCents / 100.0:0.00}。"
                + $"你确认少找了 ¥{r.ChangeCents / 100.0:0.00}，盘点差额已单独记为待退款；这不是商品利润。",
                Choose("立即联系并补还", s => s.RefundEventChange(r.Key) ? "差额已补还，待退款已结清。" : "暂未补还，待退款仍保留，请到对应店铺处理。"),
                Choose("联系顾客，约一小时后来取", _ => { r.RefundAt = Clock.Now.TotalMinutes + 60; return "已约顾客回来取款；请在这家门店等候并留足现金，到时结清差额。"; }),
                Choose("暂不处理", _ => "待退款继续保留，未成为商品收入；拖延两天会影响该店口碑。"));
        }
        public bool RefundEventChange(string key)
        {
            var r = _eventPaidReceipts.FirstOrDefault(x => x.Key == key && x.ChangeRecorded && !x.Refunded); var shop = ReceiptShop(r);
            if (r == null) return false;
            if (shop != null && CurrentPlace?.Id != shop.Place.Id) return Fail("请到待退款对应的门店处理。");
            if (!Ledger.TryDebit(Clock.Today, "退还找零差额", Money.FromCents(r.ChangeCents), r.Product,
                LedgerAttribution.ForShop(r.ShopId, r.Key + ":refund", "退款"))) return Fail("现金不足，待退款仍保留。");
            r.Refunded = true; if (shop != null) shop.Sim.AdjustShopReputation(1); Player.Charm = Math.Min(100, Player.Charm + 1); return true;
        }
        private void TickCustomerBatch(long now)
        {
            foreach (var r in _eventPaidReceipts.Where(r => r.ChangeRecorded && !r.Refunded).ToArray())
            {
                var shop = ReceiptShop(r);
                if (r.RefundAt > 0 && now >= r.RefundAt && shop != null && CurrentPlace?.Id == shop.Place.Id && shop.Sim.IsOpen && Ledger.CanAfford(Money.FromCents(r.ChangeCents)))
                { if (RefundEventChange(r.Key)) Say("顾客已按约取回找零差额。"); }
                if (!r.Refunded && !r.ComplaintApplied && Clock.Today.TotalDays >= r.ChangeDay + 2 && shop != null)
                { r.ComplaintApplied = true; shop.Sim.AdjustShopReputation(-2); Say("未处理的找零差额引来顾客投诉，该店口碑 −2，仍需退还。"); }
            }
        }
        private EventDef BindCommunityBulkOrder(EventDef template)
        {
            var shop = FindReferralShop(10); if (shop == null) return null;
            var quote = QuoteIndustryReferral(shop, 10); if (quote == null) return null;
            quote.Id = "community-bulk:" + shop.Place.Id + ":" + Clock.Now.TotalMinutes;
            quote.Client = "社区团购团长"; quote.Introducer = null; quote.CommunityOrder = true;
            string Record(int bps)
            {
                if (ShopAt(shop.Place.Id) != shop || shop.RetailKind != quote.RetailKind || _cityReferrals.Any(o => o.Id == quote.Id)) return "门店已变化或订单已处理。";
                quote.PriceBasisPoints = bps;
                if (bps == 10000)
                {
                    double chance = EventScript.ProgressiveChance(.5, (EffectiveCharm, 45, .01), (Player.Intelligence, 50, .01));
                    if (!Random.Chance(chance)) return $"原价成交成功率 {chance:P0}。对方未接受，本次未下单、无收入或库存扣除。";
                }
                _cityReferrals.Add(quote);
                return "已记录真实十件订单，报价 " + quote.Total + "。请在门店订单／城市履历确认接单，再实际备货交付；当前没有收入。";
            }
            return EventScene(template, $"社区团长向{ShopTitle(shop)}询价：{quote.ProductName} ×10，原价合计 {quote.Total}。"
                + "按店型备货、装机或制作，全量交付后才入账；价格按真实商品和折扣计算。",
                Choose("报九五折 · " + Money.FromCents(quote.FullPriceCents).Scale(.95), _ => Record(9500)),
                Choose("报九折 · " + Money.FromCents(quote.FullPriceCents).Scale(.9), _ => Record(9000)),
                Choose("坚持原价 · 低于魅力45或智力50必定谈失败", _ => Record(10000)),
                Choose("这单不接", _ => "本次拒单，没有钱款和库存变化。"));
        }
        private void SaveCustomerBatch(SaveData d) => d.eventPaidReceipts = _eventPaidReceipts.Select(r => r.Copy()).ToArray();
        private void RestoreCustomerBatch(SaveData d) { _eventPaidReceipts.Clear(); _eventPaidReceipts.AddRange((d.eventPaidReceipts ?? Array.Empty<EventPaidReceipt>()).Select(r => r.Copy())); }
    }
    public sealed partial class SaveData { public EventPaidReceipt[] eventPaidReceipts; }
}
