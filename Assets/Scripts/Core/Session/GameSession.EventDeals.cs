using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Time;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        private const string JieRepaymentMemo = "唐俊杰还钱（含谢礼）";
        private const string LinRepaymentMemo = "林远航还钱（含请客）";
        private const string MingRepaymentMemo = "许明归还周转款";
        internal bool HasJieLoan => _scheduledCredits.Any(credit => credit.memo == JieRepaymentMemo);
        internal bool HasLinLoan => _linLoan != null || _scheduledCredits.Any(credit => credit.memo == LinRepaymentMemo);

        [Serializable]
        public sealed class LinLoanState
        {
            public int DueDay;
            public double Chance;
            public bool Overdue, Extended, Reminded;
            public LinLoanState Copy() => (LinLoanState)MemberwiseClone();
        }

        private LinLoanState _linLoan;

        private EventDef BindZhouClearance(EventDef template)
        {
            var shops = MyShops.Where(shop => shop.RetailKind == "computer")
                .OrderBy(shop => shop.Place == CurrentPlace ? 0 : 1).ToArray();
            if (shops.Length == 0) return null;
            var gpu = PartCatalog.Get("gpu1");
            var ram = PartCatalog.Get("ram1");
            var price = Money.FromYuan(1600);
            var retail = Market.PriceOf(gpu) + Market.PriceOf(ram) * 2;
            var contents = new[] { (Part: gpu, Count: 1), (Part: ram, Count: 2) };
            double space = contents.Sum(line => Stock.SpaceOf(line.Part, true, line.Count));
            bool CanReceive(OwnedShop shop)
            {
                if (ShopAt(shop.Place.Id) != shop || shop.RetailKind != "computer") return false;
                double incoming = 0;
                WithShop(shop, () => incoming = IncomingShopSpace);
                return shop.Sim.Stock.FreeSpace - incoming + 1e-9 >= space;
            }
            string Order(OwnedShop shop)
            {
                if (!CanReceive(shop) || !Ledger.CanAfford(price)) return "仓位或现金已变化，这笔单没有成交。";
                var arrival = GameTimestamp.From(Clock.Today.AddDays(1), 600);
                string orderId = "zhou-clearance:" + _cityProcurementSequence++;
                var gpuPrice = price.Scale(Market.PriceOf(gpu).Cents / (double)retail.Cents);
                var prices = new[] { gpuPrice, price - gpuPrice };
                for (int i = 0; i < contents.Length; i++)
                {
                    var line = contents[i];
                    Ledger.ForceDebit(Clock.Today, "进货", prices[i], "周维成清仓 · " + line.Part.Name,
                        LedgerAttribution.ForShop(shop.Place.Id, orderId + ":" + line.Part.Id, "采购")
                            .Product(line.Part.Id, line.Count, prices[i].Cents / line.Count));
                    _directShipments.Add(new DirectShipment { OrderId = orderId, PartId = line.Part.Id, Count = line.Count,
                        Arrival = arrival.TotalMinutes, ShopId = shop.Place.Id });
                }
                RegisterProcurementOrder(orderId, "清仓", shop.Place.Id, "direct:zhou", price.Cents, arrival.TotalMinutes,
                    contents.Select(line => new PurchaseLine { Kind = PurchaseGoodsKind.Part, PartId = line.Part.Id,
                        Count = line.Count, SourceId = "direct:zhou" }).ToArray());
                AddRelation(Npcs.Zhou, 4);
                return $"已付 {price}，货物将于 {arrival} 送到{ShopTitle(shop)}。交情 +4。";
            }
            var choices = shops.Select(shop => Choose($"送到{ShopTitle(shop)} · {price}", _ => Order(shop),
                s => s.Ledger.CanAfford(price) && CanReceive(shop), "现金或可用仓位不足")).ToList();
            choices.Add(Choose("这次算了", _ => "周维成说，有合适的货再联系。"));
            string comparison = retail > price ? $"比今日市价低 {retail - price}。" : $"今日市价 {retail}。";
            return EventScene(template, $"周维成把 {gpu.Name} ×1 和 {ram.Name} ×2 打包卖 {price}。{comparison}次日 10:00 送店。", choices.ToArray());
        }

        internal OwnedShop JieTradeShop() => EventRetailShops().FirstOrDefault(shop =>
            shop.RetailKind == "computer" && EventShelfProducts(shop).Any(product => product.Part != null));

        private EventDef BindJieTrade(EventDef template)
        {
            var shop = JieTradeShop();
            if (shop == null) return null;
            var products = EventShelfProducts(shop).Where(product => product.Part != null).ToArray();
            var product = products[Random.Next(0, products.Length)];
            var price = EventPrice(shop, product).Scale(1.10);
            string label = product.Name + (product.Boxed ? "（盒装）" : "（散件）");
            return EventScene(template, $"唐俊杰想从{ShopTitle(shop)}收一件{label}，报价 {price}（当前零售价的 110%）。",
                Choose($"卖给他 · {price}", s =>
                {
                    if (s.ShopAt(shop.Place.Id) != shop || shop.RetailKind != "computer"
                        || !s.EventShelfProducts(shop).Any(line => line.Id == product.Id && line.Boxed == product.Boxed && line.Count > 0)
                        || !RemoveEventProduct(shop, product, 1)) return "这件配件已经不在货架上，本次未成交。";
                    shop.Sim.RecordBulkSale(label, 1, price, "唐俊杰调货");
                    s.AddRelation(Npcs.Jie, 3);
                    return $"已交付{label}，实收 {price}。交情 +3。";
                }),
                Choose("留在店里卖", _ => "唐俊杰说他再去问问。"));
        }

        private EventDef BindJieLoan(EventDef template)
        {
            if (HasJieLoan) return null;
            var principal = Money.FromYuan(1000);
            return EventScene(template, "唐俊杰还差一笔进货款，想借 ¥1,000。十天后连同 ¥100 谢礼一起还。",
                Choose("借 ¥1,000 · 10 天后收回 ¥1,100", s =>
                {
                    if (s.HasJieLoan || !s.Ledger.TryDebit(s.Clock.Today, "借款借出", principal, "借给唐俊杰")) return "借款未登记。";
                    s.AddRelation(Npcs.Jie, 8);
                    return "已借出 ¥1,000。交情 +8。\n" + s.ScheduleNpcRepayment(10, Money.FromYuan(1100), JieRepaymentMemo);
                }, s => !s.HasJieLoan && s.Ledger.CanAfford(principal), "有未还借款或现金不足"),
                Choose("手头也紧，改天再说", _ => "唐俊杰点点头，打算去找别的办法。"));
        }

        private bool PersonHasWork(string id) => !string.IsNullOrEmpty(CityPersonState(id)?.WorkPlaceId)
            || Employees.Any(row => PersonIdOf(row.Person) == id);

        private EventDef BindLinVisit(EventDef template)
        {
            string body = PersonHasWork(Npcs.Lin) ? "林远航打了个招呼，说最近工作上有点烦，想找个时间和你聊聊。"
                : "林远航打了个招呼，说最近找活儿不太顺，想找个时间和你聊聊。";
            return EventScene(template, body,
                Choose("约个时间一起吃饭", s => s.ScheduleLegacyCityMeeting(Npcs.Lin, "dinner", "legacy-lin-meal:" + s.Clock.Today.TotalDays)),
                Choose("寒暄两句，先去忙", _ => "你们简单打了个招呼，各自去忙了。"));
        }

        private EventDef BindLinLoan(EventDef template)
        {
            if (HasLinLoan) return null;
            var principal = Money.FromYuan(2000);
            double chance = EventScript.ProgressiveChance(.65, (Relation(Npcs.Lin), 70, .01));
            return EventScene(template, "林远航想借 ¥2,000 报培训班，约定 25 天后还 ¥2,200。",
                Choose($"借 ¥2,000 · 按时还款概率 {chance * 100:0}%", s =>
                {
                    if (s.HasLinLoan || !s.Ledger.TryDebit(s.Clock.Today, "借款借出", principal, "借给林远航")) return "借款未登记。";
                    s._linLoan = new LinLoanState { DueDay = s.Clock.Today.TotalDays + 25, Chance = chance };
                    s.AddRelation(Npcs.Lin, 10);
                    return $"已借出 ¥2,000。约定 {new GameDate(s._linLoan.DueDay)} 还款，已记入日程。交情 +10。";
                }, s => !s.HasLinLoan && s.Ledger.CanAfford(principal), "有未还借款或现金不足"),
                Choose("这次帮不了", _ => "林远航说他再想想别的办法。"));
        }

        private void CheckLinRepayment(GameDate today)
        {
            if (_linLoan == null || _linLoan.Overdue || today.TotalDays < _linLoan.DueDay) return;
            if (Random.Chance(_linLoan.Chance)) ReceiveLinRepayment();
            else
            {
                _linLoan.Overdue = true;
                Show("林远航的借款到期", "还没收到还款。空下来可以联系他、给些宽限，或记为收不回。");
            }
        }

        private string ReceiveLinRepayment()
        {
            _linLoan = null;
            Ledger.Credit(Clock.Today, NpcRepaymentTitle, Money.FromYuan(2200), LinRepaymentMemo);
            return "林远航已还款 ¥2,200。";
        }

        private bool TryShowLinOverdue()
        {
            var loan = _linLoan;
            if (loan?.Overdue != true || EventRaised == null) return false;
            var choices = new List<EventChoice>();
            if (!loan.Reminded)
                choices.Add(Choose($"催款一次 · 成功率 {loan.Chance * 100:0}%", s =>
                {
                    loan.Reminded = true;
                    return s.Random.Chance(loan.Chance) ? s.ReceiveLinRepayment() : "林远航暂时还拿不出钱。";
                }));
            if (!loan.Extended)
                choices.Add(Choose("再宽限 3 天", s =>
                {
                    loan.Extended = true;
                    loan.Overdue = false;
                    loan.DueDay = s.Clock.Today.TotalDays + 3;
                    return $"新的还款日是 {new GameDate(loan.DueDay)}。";
                }));
            choices.Add(Choose("记为收不回", s => { s._linLoan = null; return "已记下这笔未收回的 ¥2,000。"; }));
            Raise(EventScene(new EventDef { Id = "lin-loan-overdue", Title = "林远航的借款逾期" }, "林远航还有一笔借款没有还。", choices.ToArray()), Clock.Today);
            return true;
        }
    }
}
