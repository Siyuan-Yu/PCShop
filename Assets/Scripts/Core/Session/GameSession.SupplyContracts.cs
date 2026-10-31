using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Shop;
using PCShopSell.Core.Time;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        private readonly List<SupplierDebt> _supplierDebts = new List<SupplierDebt>();
        private readonly List<DirectShipment> _directShipments = new List<DirectShipment>();
        private readonly List<EnterpriseContract> _contracts = new List<EnterpriseContract>();
        private int _nextDebtId = 1, _lastOfferWeek = -1;
        public bool DirectAgreement { get; private set; }
        public IReadOnlyList<SupplierDebt> SupplierDebts => _supplierDebts;
        public IReadOnlyList<DirectShipment> DirectShipments => _directShipments;
        public IReadOnlyList<EnterpriseContract> EnterpriseContracts => _contracts;
        public Money SupplierDebtTotal => Money.FromCents(_supplierDebts.Sum(d => d.Principal + d.Fees));
        public Money CreditLimit => Money.FromYuan(Relation(Npcs.Zhou) >= 70 ? 50000 : 20000);
        public bool HasOverdueDebt => _supplierDebts.Any(d => Clock.Today.TotalDays >= d.DueDay);
        public Money UpcomingBusinessPayments(int days)
        {
            int horizon = Math.Max(0, Math.Min(3650, days));
            int today = Clock.Today.TotalDays;
            Money total = Money.FromCents(_supplierDebts.Where(x => x.DueDay <= today + horizon).Sum(x => x.Principal + x.Fees));
            foreach (var p in _properties)
                total += MortgagePayments(p.MortgageCents, p.MonthlyPrincipalCents, p.PriceCents, today, horizon);
            total += MortgagePayments(HomeMortgageCents, HomeMonthlyPrincipalCents, HomePriceCents, today, horizon);
            return total;
        }
        private static Money MortgagePayments(long debt, long installment, long price, int today, int horizon)
        {
            Money total = Money.Zero;
            int next = 30 - today % 30;
            for (int offset = next; offset <= horizon; offset += 30)
            {
                total += Money.FromCents(price).Scale(.001);
                long principal = Math.Min(debt, installment);
                total += Money.FromCents(principal) + Money.FromCents(debt).Scale(.004);
                debt -= principal;
            }
            return total;
        }
        public string DirectAgreementBlocker => !HasShop ? "先租下主店" : !HasSmartphone ? "需要智能手机" : PlayerShop.Reputation < 10 ? "知名度达到 10" : Relation(Npcs.Zhou) < 40 ? "与老周交情达到 40" : null;
        public bool SignDirectAgreement()
        {
            if (IsGameOver || DirectAgreement) return false;
            if (DirectAgreementBlocker != null) return Fail(DirectAgreementBlocker);
            DirectAgreement = true;
            Say("老周牵线签下直供：每个型号至少 3 件，市场价 84%，次日 10:00 送店。可付 30% 首款，其余 14 天内结清。");
            return true;
        }
        public Money DirectPrice(PartDef p) => Market.PriceOf(p).Scale(.84);
        public double IncomingShopSpace => Online.PendingSpace() + _directShipments.Sum(x => Stock.SpaceOf(PartCatalog.Get(x.PartId), true, x.Count))
            + Couriers.Where(HeadsToThisShop).Sum(x => x.Items.Where(i => i.Kind == CourierItemKind.Part && PartCatalog.Exists(i.Id)).Sum(i => Stock.SpaceOf(PartCatalog.Get(i.Id), i.Boxed, i.Count)));
        public string DirectOrderBlocker(string partId, int count, bool credit)
        {
            if (!DirectAgreement || !HasShop) return "先签直供协议，并保有主店";
            if (!HasSmartphone) return "需要智能手机下单";
            if (!PartCatalog.Exists(partId) || count < 3 || count > 30) return "每个型号每批 3–30 件";
            if (HasOverdueDebt) return "有到期未付货款，先还清再进货";
            var p = PartCatalog.Get(partId);
            if (PlayerShop.Stock.FreeSpace - IncomingShopSpace + 1e-9 < Stock.SpaceOf(p, true, count)) return "仓位不足（已计入所有在途货物）";
            Money total = DirectPrice(p) * count;
            Money now = credit ? total.Scale(.3) : total;
            if (credit && SupplierDebtTotal + total - now > CreditLimit) return "超出直供信用额度";
            if (Ledger.Balance < now) return "现金不足以支付本次货款";
            return null;
        }
        public bool OrderDirect(string partId, int count, bool credit)
        {
            if (IsGameOver) return false;
            string why = DirectOrderBlocker(partId, count, credit);
            if (why != null) return Fail(why);
            Money total = DirectPrice(PartCatalog.Get(partId)) * count;
            Money paid = credit ? total.Scale(.3) : total;
            Ledger.ForceDebit(Clock.Today, "进货", paid, "直供首付/现款 " + partId);
            if (credit) _supplierDebts.Add(new SupplierDebt { Id = _nextDebtId++, Principal = (total - paid).Cents, DueDay = Clock.Today.TotalDays + 14, LastFeeDay = Clock.Today.TotalDays + 14 });
            _directShipments.Add(new DirectShipment { PartId = partId, Count = count, Arrival = GameTimestamp.From(Clock.Today.AddDays(1), 600).TotalMinutes, ShopId = PlayerShopPlace.Id });
            Flags.Add("tutorial:direct");
            Say($"直供下单 {count} 件，总额 {total}，现付 {paid}。次日 10:00 到货。" + (credit ? "尾款已列入负债。" : ""));
            return true;
        }
        public bool RepaySupplier(int id)
        {
            var d = _supplierDebts.Find(x => x.Id == id);
            if (d == null || IsGameOver) return false;
            if (!Ledger.TryDebit(Clock.Today, "偿还货款", d.Amount, "直供账单 #" + id)) return Fail("现金不足以结清这笔货款。");
            _supplierDebts.Remove(d);
            Say("直供账单已结清。");
            return true;
        }
        private void DeliverDirect(GameTimestamp now)
        {
            foreach (var x in _directShipments.Where(x => x.Arrival <= now.TotalMinutes).ToArray())
            {
                OwnedShop dest = DeliveryShop(string.IsNullOrEmpty(x.ShopId) ? null : x.ShopId);
                dest.Sim.Stock.ForceAdd(PartCatalog.Get(x.PartId), true, x.Count);
                _directShipments.Remove(x);
                Say($"直供到货：{PartCatalog.Get(x.PartId).Name} ×{x.Count}，已入{(MyShopCount > 1 ? ShopTitle(dest) : "店里")}后仓。");
            }
        }
        private void SettleSupplierDebts(int day)
        {
            foreach (var d in _supplierDebts.ToArray())
            {
                if (day < d.DueDay) continue;
                if (Ledger.Balance >= d.Amount) { RepaySupplier(d.Id); continue; }
                // 每笔每天最多一次，逾期费用最多为本金的 10%；读档不重复结算。
                if (day > d.LastFeeDay)
                {
                    d.Fees = Math.Min(d.Principal / 10, d.Fees + Math.Max(1, d.Principal / 1000) * (day - d.LastFeeDay));
                    d.LastFeeDay = day;
                }
                if (day == d.DueDay || (day - d.DueDay) % 7 == 0) Say($"直供账单 #{d.Id} 到期未付 {d.Amount}；供货暂停，逾期每日本金 0.1%，累计封顶 10%。");
                if (day >= d.DueDay + 30)
                {
                    Ledger.ForceDebit(Clock.Today, "偿还货款", d.Amount, "直供逾期 30 天强制结算");
                    _supplierDebts.Remove(d);
                    DirectAgreement = false;
                    Say("直供账款逾期 30 天已强制扣款，合作解除。负现金按破产规则处理。");
                }
            }
        }
        public void RefreshEnterpriseOffer()
        {
            int week = Clock.Today.TotalDays / 7;
            if (IsGameOver || !HasShop || PlayerShop.Reputation < 8 || _lastOfferWeek == week) return;
            _lastOfferWeek = week;
            _contracts.RemoveAll(x => !x.Accepted);
            int tier = week % 3, count = PlayerShop.Reputation >= 25 ? 5 : 3;
            Money price = PlayerShop.QuoteBuild(tier).Scale(1.04);
            _contracts.Add(new EnterpriseContract { Id = "enterprise:" + week, Client = new[] { "社区培训中心", "北街设计工作室", "青禾电商公司" }[tier], Tier = tier, Count = count, UnitCents = price.Cents, OfferUntilDay = week * 7 + 7 });
            while (_contracts.Count(x => x.Settled) > 12) _contracts.Remove(_contracts.First(x => x.Settled));
        }
        public string EnterpriseBlocker(EnterpriseContract c)
        {
            if (c == null || c.Accepted || Clock.Today.TotalDays >= c.OfferUntilDay) return "本期招标已结束";
            if (!HasShop || !HasSmartphone) return "需要主店和智能手机";
            if (_contracts.Any(x => x.Accepted && !x.Settled)) return "先完成手上的企业合同";
            if (PlayerShop.Reputation < 8) return "知名度达到 8";
            if (Ledger.Balance < c.Total.Scale(.05)) return "需预留合同额 5% 的履约保证金";
            return null;
        }
        public bool AcceptEnterprise(string id)
        {
            if (IsGameOver) return false;
            var c = _contracts.Find(x => x.Id == id);
            string why = EnterpriseBlocker(c);
            if (why != null) return Fail(why);
            c.DepositCents = c.Total.Scale(.05).Cents;
            Ledger.ForceDebit(Clock.Today, "履约保证金", Money.FromCents(c.DepositCents), c.Client);
            c.Accepted = true;
            c.DueMinute = Clock.Now.AddMinutes((c.Count + 2) * GameTimestamp.MinutesPerDay).TotalMinutes;
            for (int i=0; i<c.Count; i++) PlayerShop.AddContractOrder(c.Id, c.Tier, Money.FromCents(c.UnitCents), new GameTimestamp(c.DueMinute));
            Say($"签下{c.Client} {c.Count} 台订单：{c.Count + 2} 天交期，逐台收款。全数履约另奖 3%；未交付部分扣对应保证金。");
            return true;
        }
        public bool CancelEnterprise(string id)
        {
            var c = _contracts.Find(x => x.Id == id && x.Accepted && !x.Settled);
            if (c == null || IsGameOver) return false;
            foreach (var o in PlayerShop.Orders.Where(x => x.ContractId == id).ToArray()) PlayerShop.CancelBuild(o.Id);
            return true;
        }
        private void ContractOrderResult(BuildOrder order, bool completed)
        {
            if (order.ContractId == null) return;
            var c = _contracts.Find(x => x.Id == order.ContractId && !x.Settled);
            if (c == null) return;
            if (completed) c.Completed++; else c.Failed++;
            if (c.Completed + c.Failed < c.Count) return;
            c.Settled = true;
            Money refund = Money.FromCents(c.DepositCents).Scale((double)c.Completed / c.Count);
            if (refund.IsPositive) Ledger.Credit(Clock.Today, "履约保证金", refund, c.Client + "履约返还");
            if (c.Failed == 0) { Flags.Add("tutorial:enterprise"); Ledger.Credit(Clock.Today, "企业奖励", c.Total.Scale(.03), c.Client); AddRelation(Npcs.Zhang, 2); }
            Say($"{c.Client}结案：交付 {c.Completed}/{c.Count} 台，保证金返还 {refund}。" + (c.Failed == 0 ? "全额履约奖金已到账。" : "未履约部分保证金已扣除。"));
        }
    }
}
