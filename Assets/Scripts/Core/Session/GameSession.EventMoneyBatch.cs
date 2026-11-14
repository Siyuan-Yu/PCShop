using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Home;
using PCShopSell.Core.Time;

namespace PCShopSell.Core.Session
{
    [Serializable] public sealed class EventBillRecord
    {
        public string Id, Kind, PropertyId; public int SignedDay, Day;
        public long ExpectedCents, CreditCents; public bool Settled;
        public EventBillRecord Copy() => (EventBillRecord)MemberwiseClone();
    }
    [Serializable] public sealed class EventScratchTicket
    {
        public int Id, PrizeYuan; public bool Paid;
        public bool[] Cleared = new bool[144];
        public EventScratchTicket Copy() { var t = (EventScratchTicket)MemberwiseClone(); t.Cleared = (bool[])Cleared.Clone(); return t; }
    }
    [Serializable] public sealed class EventPrivateInvestment
    {
        public int Id, Intelligence; public long At; public double Chance; public bool Success, Settled;
        public EventPrivateInvestment Copy() => (EventPrivateInvestment)MemberwiseClone();
    }
    public sealed class EventIdleItem
    {
        public string Id, Name; public bool Furniture; public int Count; public Money Price;
    }
    public sealed partial class GameSession
    {
        private readonly List<EventBillRecord> _eventBills = new List<EventBillRecord>();
        private readonly List<EventPrivateInvestment> _eventInvestments = new List<EventPrivateInvestment>();
        private readonly Dictionary<string, long> _eventBillPayments = new Dictionary<string, long>();
        private int _eventBillPaymentRevision = -1, _eventBillPaymentCount = -1;
        private EventScratchTicket _eventTicket;
        private long _hospitalPolicyUntil;
        private int _moneyEventSequence;
        public const int HospitalPolicyYuan = 3000;
        public bool HasHospitalCoverage => _hospitalPolicyUntil > Clock.Now.TotalMinutes;
        public long HospitalPolicyUntil => _hospitalPolicyUntil;
        public EventScratchTicket ScratchTicket => _eventTicket?.Copy();
        public EventPrivateInvestment[] PrivateInvestments => _eventInvestments.Select(i => i.Copy()).ToArray();
        public EventBillRecord[] EventBills => _eventBills.Select(b => b.Copy()).ToArray();
        public Money EventMoneyAssets => Money.FromCents(_eventBills.Sum(b => b.CreditCents + (!b.Settled ? BillOverpayment(b) : 0))
            + _eventInvestments.Where(i => !i.Settled).Sum(i => 1000000L));
        public static double PrivateInvestmentChance(int intelligence) => intelligence < 50 ? 0 : Math.Min(.8, .3 + (intelligence - 50) * .01);
        private bool BillContractValid(EventBillRecord b)
        {
            if (b.Kind == "rent-shop") return ShopAt(b.PropertyId)?.Lease?.SignedDay == b.SignedDay
                || PendingLease?.PlaceId == b.PropertyId && PendingLease.SignedDay == b.SignedDay;
            if (b.Kind == "utilities") return Home.Id == b.PropertyId;
            return RentedResidences.Any(p => p.Id == b.PropertyId && ResidentialLeaseSignedDay(p) == b.SignedDay);
        }
        private static string BillCreditKey(string kind, string property, int signed) => kind + "|" + property + "|" + signed;
        private long BillOverpayment(EventBillRecord b)
        {
            // Net worth and HUD queries must not rescan the entire ledger for every historical invoice.
            if (_eventBillPaymentRevision != Ledger.Revision || _eventBillPaymentCount != _eventBills.Count)
            {
                _eventBillPayments.Clear(); var invoices = _eventBills.ToDictionary(x => x.Id);
                foreach (var e in Ledger.Entries)
                {
                    if (!e.Amount.IsNegative || e.SourceEventId == null || e.Category is not ("水电" or "房租")) continue;
                    int extra = e.SourceEventId.IndexOf(":extra:", StringComparison.Ordinal);
                    string key = extra < 0 ? e.SourceEventId : e.SourceEventId.Substring(0, extra);
                    if (!invoices.TryGetValue(key, out var bill) || e.Attribution?.PropertyId != bill.PropertyId
                        || e.Category != (bill.Kind == "utilities" ? "水电" : "房租")) continue;
                    _eventBillPayments.TryGetValue(key, out long paid); _eventBillPayments[key] = paid - e.Amount.Cents;
                }
                _eventBillPaymentRevision = Ledger.Revision; _eventBillPaymentCount = _eventBills.Count;
            }
            _eventBillPayments.TryGetValue(b.Id, out long total); return Math.Max(0, total - b.ExpectedCents);
        }
        public long EventBillRefundDue(string id)
        { var b = _eventBills.FirstOrDefault(b => b.Id == id && !b.Settled); return b == null ? 0 : BillOverpayment(b); }
        // The invoice stores the expected charge at billing time. Later furniture/rent changes never rewrite it.
        private Money ChargeRecordedBill(string kind, string property, int signed, string source, Money amount, string category, string memo, LedgerAttribution attribution)
        {
            if (_eventBills.Any(b => b.Id == source) || Ledger.HasSource(source)) return Money.Zero;
            long credit = 0;
            foreach (var b in _eventBills.Where(b => b.Kind == kind && b.PropertyId == property && b.SignedDay == signed && b.CreditCents > 0))
            { long used = Math.Min(amount.Cents - credit, b.CreditCents); b.CreditCents -= used; credit += used; if (credit >= amount.Cents) break; }
            var net = Money.FromCents(amount.Cents - credit);
            if (net.IsPositive) Ledger.ForceDebit(Clock.Today, category, net, memo, new LedgerAttribution(attribution.BusinessKind, attribution.ShopId, property,
                sourceEventId: source, transactionKind: attribution.TransactionKind));
            _eventBills.Add(new EventBillRecord { Id = source, Kind = kind, PropertyId = property, SignedDay = signed, Day = Clock.Today.TotalDays, ExpectedCents = net.Cents });
            return net;
        }
        private Dictionary<string, long> ForecastBillCredits() => _eventBills.Where(b => b.CreditCents > 0 && BillContractValid(b))
            .GroupBy(b => BillCreditKey(b.Kind, b.PropertyId, b.SignedDay)).ToDictionary(g => g.Key, g => g.Sum(b => b.CreditCents));
        private static Money ForecastBillNet(Dictionary<string, long> credits, string kind, string property, int signed, Money gross)
        {
            string key = BillCreditKey(kind, property, signed); credits.TryGetValue(key, out long have);
            long used = Math.Min(have, gross.Cents); credits[key] = have - used; return Money.FromCents(gross.Cents - used);
        }
        public bool SettleEventBill(string id, bool creditNext)
        {
            var b = _eventBills.FirstOrDefault(b => b.Id == id && !b.Settled); if (b == null) return false;
            long cents = BillOverpayment(b); if (cents <= 0) return false;
            if (creditNext && !BillContractValid(b)) return Fail("原租约已结束，请选择退回现金。");
            b.Settled = true;
            if (creditNext) b.CreditCents = cents;
            else Ledger.Credit(Clock.Today, b.Kind == "utilities" ? "水电退款" : "房租退款", Money.FromCents(cents), b.PropertyId,
                new LedgerAttribution(b.Kind == "rent-shop" ? LedgerBusinessKind.Operating : LedgerBusinessKind.Personal,
                    b.Kind == "rent-shop" ? b.PropertyId : null, b.PropertyId, sourceEventId: "bill-refund:" + b.Id, transactionKind: "账单退款"));
            return true;
        }
        internal bool CanOfferMoneyBatch(int n) => n switch {
            101 => _eventBills.Any(b => b.Kind == "utilities" && !b.Settled && BillOverpayment(b) > 0),
            103 => _eventTicket == null || _eventTicket.Paid,
            105 => _eventBills.Any(b => b.Kind.StartsWith("rent-", StringComparison.Ordinal) && !b.Settled && BillOverpayment(b) > 0),
            106 => !HasHospitalCoverage || _hospitalPolicyUntil - Clock.Now.TotalMinutes <= 30 * 1440L,
            107 => !_eventInvestments.Any(i => !i.Settled),
            109 => City.HasPlace(OldMarketVenueId) && EventIdleItems.Length > 0,
            _ => false };
        private EventDef BindMoneyBatch(EventDef template, int n)
        {
            if (!CanOfferMoneyBatch(n)) return null;
            if (n is 101 or 105)
            {
                var b = _eventBills.First(b => !b.Settled && (n == 101 ? b.Kind == "utilities" : b.Kind.StartsWith("rent-", StringComparison.Ordinal)) && BillOverpayment(b) > 0);
                string place = City.HasPlace(b.PropertyId) ? City.GetPlace(b.PropertyId).Name : b.PropertyId;
                return EventScene(template, place + "在" + new GameDate(b.Day) + "的账单已核实多付" + Money.FromCents(BillOverpayment(b)) + "。",
                    Choose("退回原款", _ => SettleEventBill(b.Id, false) ? "已按实际多付款退回，账单已结清。" : "这笔差额已处理。"),
                    Choose("抵扣下期账单", _ => SettleEventBill(b.Id, true) ? "已记入本处账单抵扣，下期实际少扣。" : "暂不能抵扣，请查看原租约。"), Choose("稍后处理", _ => "待退款仍保留，可从经营待办处理。"));
            }
            if (n == 103)
                return EventScene(template, "刮刮乐¥200一张：80%未中、12%中¥200、7%中¥1,000、1%中¥10,000。买下后自己刮开。",
                    Choose("买一张 · ¥200", _ => BuyEventScratchTicket() ? "已买下，打开经营待办刮开彩票。" : "没有买下，请检查现金或已有彩票。"), Choose("不买", _ => "没有消费。"));
            if (n == 106)
                return EventScene(template, "住院保障¥3,000/年。保障期内入院，报销实际住院费用80%；不限次数，没有单次或累计上限。门诊不在保障内。",
                    Choose(HasHospitalCoverage ? "续保一年 · ¥3,000" : "投保一年 · ¥3,000", _ => BuyHospitalPolicy() ? "保障已生效，有效至" + new GameTimestamp(_hospitalPolicyUntil) + "，到期需自行续费。" : "现金不足，未投保。"),
                    Choose("查看条款", _ => "一次投保保障360天；有效期内入院按住院实收费用报销80%，不封顶。到期不自动扣款、不继续承保；提前续保从原到期日接续。"), Choose("暂不购买", _ => "没有扣款。"));
            if (n == 107)
            {
                double chance = PrivateInvestmentChance(Player.Intelligence);
                return EventScene(template, "朋友推荐一笔高风险私人投资：投入¥10,000，30天后成功总回款¥20,000，失败本金全失。当前智力" + Player.Intelligence + "，成功率" + chance.ToString("P0") + "。",
                    Choose("核验风险和约定", _ => "没有保本承诺。智力不足50必败，50时成功率30%，之后每点+1个百分点，最高80%。这不是银行存款。"),
                    Choose("投入¥10,000 · 30天后结算", _ => StartPrivateInvestment() ? "已投入¥10,000，到期结果和投入时的成功率已记录。" : "现金不足或已有待到期投资。"), Choose("不参与", _ => "没有投入或损失。"));
            }
            return EventScene(template, "你有可处理的随身旧物，可以带去青湾旧物市场，按实物和数量出售。",
                Choose("看看可卖的闲置", _ => { Flags.Add("event-idle-sale"); return "已加入经营待办。随身闲置列表显示实物、数量与回收单价，到市场才交货收款。"; }), Choose("先留着", _ => "物品原样保留，没有收入。"));
        }
        public bool BuyEventScratchTicket()
        {
            if (IsGameOver || _eventTicket != null && !_eventTicket.Paid || !Ledger.TryDebit(Clock.Today, "刮刮乐", Money.FromYuan(200), "购买一张刮刮乐")) return false;
            int roll = Random.Next(0, 100), prize = roll < 80 ? 0 : roll < 92 ? 200 : roll < 99 ? 1000 : 10000;
            _eventTicket = new EventScratchTicket { Id = ++_moneyEventSequence, PrizeYuan = prize }; return true;
        }
        public bool ScratchEventCell(int cell)
        {
            if (_eventTicket == null || _eventTicket.Paid || cell < 0 || cell >= 144 || _eventTicket.Cleared[cell]) return false;
            _eventTicket.Cleared[cell] = true; return true;
        }
        public bool CanClaimScratch => _eventTicket != null && !_eventTicket.Paid && _eventTicket.Cleared.Count(c => c) >= 87;
        public bool ClaimEventScratch()
        {
            if (!CanClaimScratch) return false;
            _eventTicket.Paid = true;
            if (_eventTicket.PrizeYuan > 0) Ledger.Credit(Clock.Today, "刮刮乐中奖", Money.FromYuan(_eventTicket.PrizeYuan), "刮刮乐#" + _eventTicket.Id,
                new LedgerAttribution(LedgerBusinessKind.Personal, sourceEventId: "scratch-prize:" + _eventTicket.Id));
            Say(_eventTicket.PrizeYuan > 0 ? "领到奖金¥" + _eventTicket.PrizeYuan + "。" : "这张没有中奖。"); return true;
        }
        public bool BuyHospitalPolicy()
        {
            if (IsGameOver || !Ledger.TryDebit(Clock.Today, "住院保险", Money.FromYuan(HospitalPolicyYuan), "住院80%费用保障一年")) return false;
            _hospitalPolicyUntil = Math.Max(_hospitalPolicyUntil, Clock.Now.TotalMinutes) + GameDate.DaysPerYear * 1440L; return true;
        }
        private Money ReimburseEventHospital(LedgerEntry charge, bool covered)
        {
            if (!covered || charge.Category != "医疗" || !charge.Amount.IsNegative || string.IsNullOrEmpty(charge.EntryId)) return Money.Zero;
            if (!Ledger.Entries.Any(e => e.EntryId == charge.EntryId && e.Category == "医疗" && e.Amount == charge.Amount && e.Memo?.Contains("住院") == true)) return Money.Zero;
            string key = "hospital-insurance:" + charge.EntryId;
            if (Ledger.HasSource(key)) return Money.Zero;
            var payout = charge.Amount.Abs().Scale(.8);
            if (payout.IsPositive) Ledger.Credit(Clock.Today, "住院保险理赔", payout, "对应医疗账单#" + charge.EntryId,
                new LedgerAttribution(LedgerBusinessKind.Personal, sourceEventId: key, transactionKind: "住院报销"));
            return payout;
        }
        public bool StartPrivateInvestment()
        {
            if (IsGameOver || _eventInvestments.Any(i => !i.Settled) || !Ledger.TryDebit(Clock.Today, "私人投资投入", Money.FromYuan(10000), "高风险私人投资30天")) return false;
            double chance = PrivateInvestmentChance(Player.Intelligence);
            _eventInvestments.Add(new EventPrivateInvestment { Id = ++_moneyEventSequence, Intelligence = Player.Intelligence, Chance = chance,
                Success = chance > 0 && Random.Chance(chance), At = Clock.Now.TotalMinutes + 30 * 1440L }); return true;
        }
        public EventIdleItem[] EventIdleItems => Backpack.FurnitureLines.Where(l => FurnitureCatalog.Get(l.Key)?.Price.IsPositive == true)
            .Select(l => new EventIdleItem { Id = l.Key, Name = FurnitureCatalog.Get(l.Key).Name, Count = l.Value, Furniture = true, Price = FurnitureCatalog.Get(l.Key).Price.Scale(.55) })
            .Concat(Backpack.GiftLines.Where(l => l.Key.StartsWith("oldmarket:", StringComparison.Ordinal))
                .Select(l => new EventIdleItem { Id = l.Key, Name = Gifts.Get(l.Key).Name, Count = l.Value, Price = OldMarketBuybackPrice(l.Key) })).ToArray();
        public bool SellEventIdle(string id, bool furniture, int count)
        {
            var item = EventIdleItems.FirstOrDefault(i => i.Id == id && i.Furniture == furniture);
            if (IsGameOver || !AtOldMarket || _oldMarket.Pending != null || item == null || count < 1 || count > item.Count) return false;
            if (!(furniture ? Backpack.TakeFurniture(id, count) : Backpack.TakeGift(id, count))) return false;
            Ledger.Credit(Clock.Today, "闲置物品出售", item.Price * count, item.Name + " ×" + count); ApplyCargoLimit();
            if (EventIdleItems.Length == 0) Flags.Remove("event-idle-sale"); return true;
        }
        private void TickMoneyBatch(long now)
        {
            foreach (var i in _eventInvestments.Where(i => !i.Settled && now >= i.At))
            {
                i.Settled = true;
                if (i.Success) Ledger.Credit(Clock.Today, "私人投资回款", Money.FromYuan(20000), "30天私人投资（含本金）",
                    new LedgerAttribution(LedgerBusinessKind.Personal, sourceEventId: "private-investment:" + i.Id));
                Say(i.Success ? "私人投资到期，收到¥20,000（含本金）。" : "私人投资到期未兑现，投入的¥10,000全部损失。");
            }
            foreach (var b in _eventBills.Where(b => b.CreditCents > 0 && !BillContractValid(b)))
            { Ledger.Credit(Clock.Today, "账单抵扣退回", Money.FromCents(b.CreditCents), "原住处或租约结束 · " + b.PropertyId); b.CreditCents = 0; }
        }
        private void SaveMoneyBatch(SaveData d)
        { d.moneyEventBills = _eventBills.Select(b => b.Copy()).ToArray(); d.moneyEventInvestments = PrivateInvestments; d.moneyEventTicket = ScratchTicket; d.moneyEventPolicyUntil = _hospitalPolicyUntil; d.moneyEventSequence = _moneyEventSequence; }
        private void RestoreMoneyBatch(SaveData d)
        {
            _eventBills.Clear(); _eventBills.AddRange((d.moneyEventBills ?? Array.Empty<EventBillRecord>()).Select(b => b.Copy()));
            _eventBillPaymentRevision = -1; _eventBillPaymentCount = -1;
            _eventInvestments.Clear(); _eventInvestments.AddRange((d.moneyEventInvestments ?? Array.Empty<EventPrivateInvestment>()).Select(i => i.Copy()));
            _eventTicket = d.moneyEventTicket?.Copy(); _hospitalPolicyUntil = d.moneyEventPolicyUntil; _moneyEventSequence = d.moneyEventSequence;
        }
    }
    public sealed partial class SaveData
    {
        public EventBillRecord[] moneyEventBills; public EventPrivateInvestment[] moneyEventInvestments; public EventScratchTicket moneyEventTicket;
        public long moneyEventPolicyUntil; public int moneyEventSequence;
    }
}
