using System;
using System.Globalization;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    public sealed partial class SaveData
    {
        public int propertyPriceWeek;
        public double[] propertyPriceFactors;
        public string propertyPriceRandom;
        public long homeLeaseRentCents;
        public int residentialWeeklyRentVersion, homeLeaseSignedDay;
    }

    public sealed partial class GameSession
    {
        // Sale and asking rent move independently; signed rents are stored on leases.
        private readonly double[] _propertyPriceFactors = { 1, 1, 1, 1 };
        private readonly GameRandom _propertyPriceRandom = new GameRandom(0xC17A55E712UL);
        private int _propertyPriceWeek;
        private long _homeLeaseRentCents = HomeRent.Cents;
        private int _homeLeaseSignedDay;
        private double HomeSaleFactor => _propertyPriceFactors[0];
        private double HomeRentFactor => _propertyPriceFactors[1];
        private double ShopSaleFactor => _propertyPriceFactors[2];
        private double ShopRentFactor => _propertyPriceFactors[3];
        private static Money HomeBaseRent(int tier) => Money.FromYuan(tier == 3 ? 12000 : tier == 2 ? 3000 : tier == 1 ? 1200 : 450);
        private static Money WeeklyResidentialRent(Money monthly) => monthly.Scale((double)GameDate.DaysPerWeek / GameDate.DaysPerMonth);

        private void InitializePropertyMarket(ulong seed)
        {
            _propertyPriceRandom.State = seed ^ 0xC17A55E712UL;
            if (_propertyPriceRandom.State == 0) _propertyPriceRandom.State = 0xC17A55E712UL;
            _propertyPriceWeek = Clock.Today.TotalDays / 7;
        }

        private void TickPropertyMarket()
        {
            int week = Clock.Today.TotalDays / 7;
            while (_propertyPriceWeek < week)
            {
                double oldShopRent = ShopRentFactor;
                for (int i = 0; i < _propertyPriceFactors.Length; i++)
                {
                    double limit = i % 2 == 0 ? .035 : .025;
                    double movement = _propertyPriceRandom.Range(-limit, limit);
                    _propertyPriceFactors[i] = Math.Max(.5, Math.Min(2, _propertyPriceFactors[i] * (1 + movement)));
                }
                foreach (var listing in _listings)
                    listing.Rent = Money.Max(Money.FromCents(1), listing.Rent.Scale(ShopRentFactor / oldShopRent));
                _propertyPriceWeek++;
            }
        }

        public Money PropertyMarketValue(OwnedProperty property)
        {
            if (!City.HasPlace(property.PlaceId)) return Money.FromCents(property.PriceCents);
            var place = City.GetPlace(property.PlaceId);
            return place.Kind == PlaceKind.Home ? ResidentialPrice(place) : CityPropertyPrice(place.Id);
        }

        private void SavePropertyMarket(SaveData data)
        {
            data.propertyPriceWeek = _propertyPriceWeek;
            data.propertyPriceFactors = (double[])_propertyPriceFactors.Clone();
            data.propertyPriceRandom = _propertyPriceRandom.State.ToString(CultureInfo.InvariantCulture);
            data.homeLeaseRentCents = _homeLeaseRentCents;
            data.homeLeaseSignedDay = _homeLeaseSignedDay;
            data.residentialWeeklyRentVersion = 1;
        }

        private void RestorePropertyMarket(SaveData data)
        {
            _propertyPriceWeek = Clock.Today.TotalDays / 7;
            if (data.propertyPriceFactors?.Length == 4 && data.propertyPriceFactors.All(v => v >= .5 && v <= 2))
                Array.Copy(data.propertyPriceFactors, _propertyPriceFactors, 4);
            _propertyPriceWeek = Math.Max(0, Math.Min(_propertyPriceWeek, data.propertyPriceWeek));
            if (ulong.TryParse(data.propertyPriceRandom, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong state) && state != 0)
                _propertyPriceRandom.State = state;
        }

        private void RestoreHomeLease(SaveData data)
        {
            bool weekly = data.residentialWeeklyRentVersion > 0;
            var savedRent = Money.FromCents(data.homeLeaseRentCents);
            Money rent = HomeTier == 0 && Home == _initialHome ? HomeRent
                : savedRent.IsPositive ? (weekly ? savedRent : WeeklyResidentialRent(savedRent))
                : WeeklyResidentialRent(HomeBaseRent(HomeTier));
            _homeLeaseRentCents = rent.Cents;
            _homeLeaseSignedDay = weekly ? Math.Max(0, Math.Min(data.homeLeaseSignedDay, Clock.Today.TotalDays)) : Clock.Today.TotalDays;
        }
    }
}
