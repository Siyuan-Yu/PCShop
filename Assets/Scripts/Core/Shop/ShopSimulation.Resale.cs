using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Time;

namespace PCShopSell.Core.Shop
{
    public sealed partial class ShopSimulation
    {
        public Func<IEnumerable<ResaleOffer>> ResaleOffers;
        private void FillResaleShelves(IReadOnlyList<ShelfFill> shelves)
        {
            var remaining = (ResaleOffers?.Invoke()??Enumerable.Empty<ResaleOffer>()).OrderBy(o=>o.Id).ToList();
            foreach (var fill in shelves)
            {
                for (int i=0; i<remaining.Count && fill.Resale.Count<fill.CapacityItems; i++)
                {
                    var offer = remaining[i];
                    if (fill.UsedKg+offer.Kg>fill.CapacityKg+1e-9) continue;
                    fill.Resale.Add(offer);
                    fill.UsedKg += offer.Kg;
                    remaining.RemoveAt(i--);
                }
                fill.WeightBlocked = fill.Resale.Count>0 && remaining.Any(o=>fill.UsedKg+o.Kg>fill.CapacityKg+1e-9);
            }
        }
        private void DecideResale(ShopCustomer c,GameTimestamp now)
        {
            var offers=ShelfFillOf(ShelfKinds.Get("secondhand")).Resale;
            if(offers.Count==0){Leave(c,LeaveReason.NoStock);return;}
            var offer=offers[Random.Next(0,offers.Count)];
            double ratio=offer.Price/(double)Math.Max(1,offer.ReferencePrice);
            if(!Random.Chance(AssistedPurchaseIntent(Math.Max(.01,Math.Min(.9,1.15-ratio*.65)))))
            {Leave(c,LeaveReason.TooExpensive);return;}
            if(!CanPay(c)){Leave(c,LeaveReason.NoCash);return;}
            c.ResaleId=offer.Id;c.ResaleName=offer.Name;c.Spend=Money.FromYuan(offer.Price);
            c.State=CustomerState.WaitingCheckout;c.WaitingSince=now;
            c.CheckoutStartedSeconds=CheckoutClock?.Invoke()??double.NaN;
            _queue.Add(c);CustomerReadyToPay?.Invoke(c);
        }
    }
}
