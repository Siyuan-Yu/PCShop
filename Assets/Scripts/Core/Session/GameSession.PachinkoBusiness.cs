using System;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        public const int PachinkoMinimumPayout = 3, PachinkoMaximumPayout = 10;
        public const int PachinkoDefaultPayout = 7;
        public const string PachinkoTransferAgencyId = "agency_s_night";
        private const int PachinkoCustomerVisitPriceYuan = 50;
        private const int CustomerBeadsPerVisit = 50;
        private const double CustomerRewardChance = .09;
        public static Money PachinkoTransferPrice => Money.FromYuan(160000);
        public bool PachinkoTransferUnlocked => DistrictAccessTier >= 3 && City.HasPlace(CityLifeCatalog.PachinkoVenue);

        public string PachinkoPurchaseBlocker
        {
            get
            {
                if (!PachinkoTransferUnlocked) return "花园片区开放后，可到花园中介购买弹珠店。";
                if (CurrentPlace?.Id != PachinkoTransferAgencyId || IsGameOver || IsWorking || CityActivityExclusive)
                    return "请到花园中介办理购买。";
                var place = City.GetPlace(CityLifeCatalog.PachinkoVenue);
                if (IsOwnedVenue(place)) return "已经接手这家店。";
                var property = PropertyAt(place.Id);
                if (property != null) return property.UseMode == "vacant" ? null : "这处商铺目前仍在使用。";
                return Ledger.CanAfford(PachinkoTransferPrice) ? null : "现金不足，转让费用 " + PachinkoTransferPrice;
            }
        }

        public bool PurchasePachinko()
        {
            if (PachinkoPurchaseBlocker is string blocker) return Fail(blocker);
            var place = City.GetPlace(CityLifeCatalog.PachinkoVenue);
            var property = PropertyAt(place.Id);
            if (property == null)
            {
                if (!Ledger.TryDebit(Clock.Today, "购置物业", PachinkoTransferPrice, $"接手{place.Name}和原机台",
                    LedgerAttribution.ForAsset(place.Id, "pachinko:transfer:" + Clock.Now.TotalMinutes))) return false;
                property = new OwnedProperty
                {
                    PlaceId = place.Id, PriceCents = PachinkoTransferPrice.Cents, BoughtDay = Clock.Today.TotalDays,
                    CitySchema = 1, LastSettledMonth = Clock.Today.TotalDays / 30
                };
                _properties.Add(property);
            }
            if (HasShop) OpenNewShop(place, Money.Zero, Money.Zero);
            else MoveInto(place, Money.Zero, Money.Zero);
            var shop = ShopAt(place);
            shop.OwnsProperty = true;
            shop.Sim.TradingEnabled = false;
            property.UseMode = "self";
            property.TenantName = "玩家自营";
            property.AskingRentCents = 0;
            VenueOperation(place.Id);
            Say($"已接手{place.Name}。可在「经营 → 店」设置机器返奖和营业时间。");
            return true;
        }

        public int PachinkoPayoutForMachine(int machine)
        {
            var state = VenueOperation(CityLifeCatalog.PachinkoVenue);
            return state?.PachinkoPayouts is { Length: 3 } values && machine >= 0 && machine < values.Length
                ? Math.Clamp(values[machine], PachinkoMinimumPayout, PachinkoMaximumPayout) : PachinkoDefaultPayout;
        }

        public double PachinkoCustomerReturnRate(int payout, long priceCents) => CustomerRewardChance
            * CustomerBeadsPerVisit * Math.Clamp(payout, PachinkoMinimumPayout, PachinkoMaximumPayout) * 100 / Math.Max(100, priceCents);

        public bool ConfigurePachinkoMachine(int machine, int payout)
        {
            var state = VenueOperation(CityLifeCatalog.PachinkoVenue);
            if (state == null || IsGameOver || machine is < 0 or >= 3 || payout is < PachinkoMinimumPayout or > PachinkoMaximumPayout) return false;
            if (state.PachinkoPayouts?.Length != 3)
                state.PachinkoPayouts = new[] { PachinkoDefaultPayout, PachinkoDefaultPayout, PachinkoDefaultPayout };
            state.PachinkoPayouts[machine] = payout;
            return true;
        }

        private double PachinkoCustomerInterest(VenueBusiness state)
        {
            double average = 0;
            for (int i = 0; i < 3; i++) average += PachinkoCustomerReturnRate(PachinkoPayoutForMachine(i), state.PriceCents) / 3;
            return Math.Clamp(.55 + average * .70, .55, 1.30);
        }

        private void PayPachinkoCustomer(VenueBusiness state, VenueCustomer customer)
        {
            int payout = PachinkoPayoutForMachine(Random.Next(0, 3));
            int hits = 0;
            for (int i = 0; i < CustomerBeadsPerVisit; i++) if (Random.Chance(CustomerRewardChance)) hits++;
            var refund = Money.FromYuan(payout * hits);
            if (!refund.IsPositive) return;
            Ledger.ForceDebit(Clock.Today, "柏青哥返奖", refund, "顾客结算返珠",
                LedgerAttribution.ForShop(state.PlaceId, customer.ReservationId + ":payout", "机台返奖"));
            state.PachinkoPaidCents += refund.Cents;
        }
    }
}
