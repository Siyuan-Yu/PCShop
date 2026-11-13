using System;
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
    public sealed partial class GameSession
    {
        private CityActivity OutdoorMarketActivity(string id) => _cityActivities.FirstOrDefault(a => a.Id == id && a.State == CityActivityPhase.Running
            && CurrentPlace?.Id == a.VenueId && OutdoorNumber(a.DefinitionId) >= 68 && OutdoorNumber(a.DefinitionId) <= 70);
        public PartDef[] OutdoorMarketParts(string activityId)
        {
            var a = OutdoorMarketActivity(activityId); if (a == null) return Array.Empty<PartDef>();
            return PartCatalog.All.Where(p => (a.DefinitionId != "social_70" || p.Category == PartCategory.Gpu) && StallSells(CurrentPlace, p)).ToArray();
        }
        public bool SelectOutdoorMarketPart(string activityId, string partId)
        {
            var a = OutdoorMarketActivity(activityId); var state = OutdoorState(a);
            if (state == null || state.Pending != null || state.OrderCount > 0 || !OutdoorMarketParts(activityId).Any(p => p.Id == partId)) return false;
            if (a.DefinitionId == "social_69" && state.QuoteCents.Length > 0) return Fail("比价开始后固定同一型号，不能混用不同商品的报价。");
            if (state.PartId != partId)
            {
                state.PartId = partId; state.Tested = false;
                if (state.Number == 70) a.Choices = a.Choices.Where(key => key != "2").ToArray();
            }
            return true;
        }
        public Money OutdoorMarketGoodsPrice(string activityId, int count)
        {
            var a = OutdoorMarketActivity(activityId); var s = OutdoorState(a);
            if (s == null || count < 1) return Money.Zero;
            var part = PartCatalog.Get(s.PartId);
            string source = s.Number == 69 && s.PurchaseSource < s.QuoteSources.Length ? s.QuoteSources[s.PurchaseSource] : null;
            return (source == Online1788.Shopping.Id ? OnlinePrice(Online1788.Shopping, part) : source == "direct:zhou" ? DirectPrice(part) : StallPrice(CurrentPlace, part)) * count;
        }
        public bool SelectOutdoorPurchaseSource(string id, int index)
        {
            var a = OutdoorMarketActivity(id); var s = OutdoorState(a);
            if (s == null || s.Number != 69 || s.Pending != null || s.OrderCount > 0 || index < 0 || index >= s.QuoteSources.Length) return false;
            s.PurchaseSource = index; return true;
        }
        public bool OrderOutdoorMarketGoods(string activityId, int count, bool credit = false)
        {
            var a = OutdoorMarketActivity(activityId); var s = OutdoorState(a);
            if (s == null || s.Pending != null || s.Visited < 1 || count < 1 || count > 30) return Fail("请先到场看货，数量为1–30件。");
            if (s.OrderCount > 0) return Fail("本场已经下过一单，继续采购请使用普通市场入口。");
            if (s.Number == 70 && (!HasOpenedComputerShop || !s.Tested)) return Fail("需要保有已开办的电脑店，并先完成这个型号的实测。");
            var part = PartCatalog.Get(s.PartId); var place = CurrentPlace;
            var shop = MyShops.FirstOrDefault(x => x.RetailKind == "computer" && !x.Sim.NotFitted);
            bool success;
            string source = s.Number == 69 && s.PurchaseSource < s.QuoteSources.Length ? s.QuoteSources[s.PurchaseSource] : null;
            if (!credit && source == Online1788.Shopping.Id)
                success = shop != null ? PlaceUnifiedOnlinePurchase(shop.Place.Id, Online1788.Shopping.Id, part.Id, count)
                    : PlaceOnlineOrder(Online1788.Shopping.Id, new[] { (part, count) }, true);
            else if (!credit && source == "direct:zhou")
            {
                if (shop == null) return Fail("协议直供需要一家可接收配件的电脑店。");
                success = PlaceUnifiedDirectPurchase(shop.Place.Id, part.Id, count);
            }
            else if (!credit)
            {
                bool delivery = SendByCourier; string dest = CourierDest;
                try
                {
                    SendByCourier = shop != null;
                    if (shop != null) SetCourierDest(shop);
                    success = BuyAtStall(place, part, count);
                }
                finally { SendByCourier = delivery; CourierDest = dest; }
            }
            else
            {
                if (s.Number != 70 || !s.CreditGranted || shop == null) return Fail("需要先在本场谈成账期。");
                if (!IsWholesalerOpen || !StallSells(place, part) || count > StallRemaining(place, part)) return Fail("档口已休息或当日实际供货不足。");
                Money total = StallPrice(place, part) * count, paid = total.Scale(.3), debt = total - paid;
                if (HasOverdueDebt || SupplierDebtTotal + debt > CreditLimit) return Fail("有逾期货款或超出当前授信额度。");
                double room = 0; WithShop(shop, () => room = shop.Sim.Stock.FreeSpace - IncomingShopSpace);
                if (room + 1e-9 < Stock.SpaceOf(part, true, count)) return Fail("目的店仓位不足，已计入在途货物。");
                string orderId = "wang-visit:" + _cityProcurementSequence++;
                if (!Ledger.TryDebit(Clock.Today, "显卡采购首付", paid, place.Name + " " + part.Name + " ×" + count,
                    LedgerAttribution.ForShop(shop.Place.Id, orderId + ":goods", "采购").Product(part.Id, count, StallPrice(place, part).Cents))) return Fail("现金不足以支付30%首款。");
                _supplierDebts.Add(new SupplierDebt { Id = _nextDebtId++, ShopId = shop.Place.Id, OrderId = orderId, Principal = debt.Cents,
                    DueDay = Clock.Today.TotalDays + 14, LastFeeDay = Clock.Today.TotalDays + 14 });
                long arrival = GameTimestamp.From(Clock.Today.AddDays(1), 600).TotalMinutes;
                _directShipments.Add(new DirectShipment { OrderId = orderId, PartId = part.Id, Count = count, Arrival = arrival, ShopId = shop.Place.Id });
                RecordStallPurchase(PurchaseDestination(place), part, count);
                RegisterProcurementOrder(orderId, "王振海看货账期", shop.Place.Id, place.Id, paid.Cents, arrival,
                    new[] { new PurchaseLine { Kind = PurchaseGoodsKind.Part, PartId = part.Id, Count = count, SourceId = place.Id } });
                success = true;
            }
            if (!success) return false;
            s.OrderCount = count;
            s.Message = part.Name + " ×" + count + " 已按实价下单；" + (credit ? "30%首款已付，14天尾款已入负债，次日10点送店。"
                : source == Online1788.Shopping.Id || source == "direct:zhou" ? "按所选渠道的实际运费与时间配送。"
                : shop != null ? "已交实际货运，运费另按路线计。" : "货已进入随身载具，请自行运回。");
            Say(s.Message); return true;
        }
    }
}
