using System;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        private const int PropertyLeaseDays = 28;
        private static readonly string[] RentalApplicantNames =
            { "程雨", "周扬", "林子宁", "秦乐", "宋知远", "许岚", "方晨", "何苗" };

        public bool OwnsResidence(Place place) =>
            place?.Kind == PlaceKind.Home && (place == Home && HomePriceCents > 0 || PropertyAt(place.Id) != null);

        public Money PropertyReferenceRent(Place place) =>
            place.Kind == PlaceKind.Home ? ResidentialRent(place) : ShopRentQuote(place);

        public Money PropertyMinimumAskingRent(Place place) =>
            Money.Max(Money.FromYuan(1), PropertyReferenceRent(place).Scale(.5));

        public Money PropertyMaximumAskingRent(Place place) => PropertyReferenceRent(place).Scale(2);

        public double PropertyRentalInterest(Place place, Money askingRent)
        {
            Money reference = PropertyReferenceRent(place);
            return !reference.IsPositive ? 0 : Math.Min(.88, .32 * Math.Pow(.09, (double)askingRent.Cents / reference.Cents - 1));
        }

        public Money PropertyRentReceivable(OwnedProperty property)
        {
            if (property == null || (property.UseMode != "leased" && property.UseMode != "npc"))
                return Money.Zero;
            if (property.UseMode == "npc" && City.GetPlace(property.PlaceId).Kind == PlaceKind.Home &&
                !HasResidentialOccupants(property.PlaceId)) return Money.Zero;
            return Money.FromCents(property.WeeklyRentCents).Scale(property.LegacyRentalDiscount ? .85 : 1);
        }

        public string PropertyListingBlocker(Place place)
        {
            if (IsGameOver || place == null) return "当前不能挂牌";
            if (DistrictAccessBlocker(place) is string locked) return locked;
            if (place == Home) return "请先保留另一处主住所";
            var property = PropertyAt(place.Id);
            if (property == null) return "你尚未买下这处房源";
            if (place == CurrentPlace) return "请先离开这处房源";
            if (place.Kind != PlaceKind.Home && !IsCommercialPremises(place)) return "此场所不开放出租";
            if (ShopAt(place.Id) != null || VentureAt(place.Id) != null || place.Id == ParkPlaceId) return "请先结束自营";
            if (property.UseMode != "vacant" || place.Kind == PlaceKind.Home && HasResidentialOccupants(place.Id))
                return "现有租约结束后才能挂牌";
            return PropertyReferenceRent(place).IsPositive ? null : "当前没有参考周租";
        }

        public bool ListPropertyForRent(string id, Money weeklyRent)
        {
            var place = City.HasPlace(id) ? City.GetPlace(id) : null;
            if (PropertyListingBlocker(place) is string blocker) return Fail(blocker);
            if (weeklyRent < PropertyMinimumAskingRent(place) || weeklyRent > PropertyMaximumAskingRent(place))
                return Fail("请按当前参考价重新设定周租。");
            var property = PropertyAt(id);
            property.AskingRentCents = weeklyRent.Cents;
            Say(place.Name + "已交给中介挂牌，周租 " + weeklyRent + "。");
            return true;
        }

        public bool UnlistProperty(string id)
        {
            var property = PropertyAt(id);
            if (IsGameOver || property == null || property.AskingRentCents <= 0) return false;
            property.AskingRentCents = 0;
            Say("已撤下挂牌。");
            return true;
        }

        public bool SetPropertyRecovery(string id, bool recover)
        {
            var property = PropertyAt(id);
            if (IsGameOver || property?.UseMode != "leased") return Fail("此租约暂不可办理收回。");
            if (recover && property.LeaseEndDay <= Clock.Today.TotalDays) EndPropertyRental(property);
            else property.RecoverAtLeaseEnd = recover;
            Say(recover ? "已办理收回，到期后的房源可自用或重新挂牌。" : "已取消到期收回。");
            return true;
        }

        private bool HasResidentialOccupants(string id) => _cityPeople.Values.Any(person =>
            person.HomePlaceId == id && person.PersonId != CityLifeCatalog.InvitationTestPersonId);

        private static uint PropertyRentalSeed(string id, int day)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char character in id) hash = (hash ^ character) * 16777619;
                return (hash ^ (uint)day) * 16777619;
            }
        }

        private void AdvancePropertyRental(OwnedProperty property, Place place, GameDate today)
        {
            int day = today.TotalDays;
            if (property.UseMode == "leased")
            {
                if ((!property.RecoverAtLeaseEnd || day < property.LeaseEndDay || property.LegacyRentalDiscount)
                    && WeeklyRentDue(day, property.CommercialRentFromDay)) CreditPropertyRent(property, today);
                if (day < property.LeaseEndDay) return;
                if (property.RecoverAtLeaseEnd || property.LegacyRentalDiscount) EndPropertyRental(property);
                else property.LeaseEndDay = day + PropertyLeaseDays;
                return;
            }
            if (property.AskingRentCents <= 0 || CityIntent is { Cancelled: false } intent && intent.TargetPlaceId == place.Id
                || PropertyListingBlocker(place) != null) return;

            Money rent = Money.FromCents(property.AskingRentCents);
            var random = new GameRandom(PropertyRentalSeed(place.Id, day));
            if (!random.Chance(PropertyRentalInterest(place, rent))) return;
            property.UseMode = "leased";
            property.WeeklyRentCents = rent.Cents;
            property.AskingRentCents = 0;
            property.CommercialRentFromDay = day;
            property.LeaseEndDay = day + PropertyLeaseDays;
            property.TenantName = RentalApplicantNames[random.Next(0, RentalApplicantNames.Length)];
            if (place.Kind == PlaceKind.Home) MoveNewRentalResidentIn(property, place);
            CreditPropertyRent(property, today);
            Money fee = AgentFeeFor(rent);
            if (fee.IsPositive)
                Ledger.ForceDebit(today, "中介费", fee, place.Name + "出租成交",
                    LedgerAttribution.ForAsset(place.Id, $"property:{place.Id}:lease:{day}:fee"));
            Say(place.Name + "已出租，签约周租 " + rent + "，中介费 " + fee + "。");
        }

        private void CreditPropertyRent(OwnedProperty property, GameDate today)
        {
            Money rent = PropertyRentReceivable(property);
            string source = $"property:{property.PlaceId}:week:{today.TotalDays}:rent";
            if (rent.IsPositive && !Ledger.HasSource(source))
                Ledger.Credit(today, "物业租金", rent, property.TenantName + "本周租金",
                    LedgerAttribution.ForAsset(property.PlaceId, source));
        }

        private void MoveNewRentalResidentIn(OwnedProperty property, Place place)
        {
            string id = $"rent_resident_{place.Id}_{property.CommercialRentFromDay}";
            var door = StandOf(place);
            property.TenantPersonId = id;
            _cityExtraDefinitions[id] = new CityPersonDefinition
            {
                Id = id, Name = property.TenantName, HomeId = place.Id, Role = "居民", Interest = "湖边散步",
                Topics = new[] { "我搬来这条街后，常到湖边散步。" }
            };
            _cityPeople[id] = new CityPersonState
            {
                PersonId = id, HomePlaceId = place.Id, Address = place.Name, LocationPlaceId = place.Id,
                X = door.X, Y = door.Y, Task = CityPersonTask.Home, TaskLabel = "在家休息"
            };
        }

        private void EndPropertyRental(OwnedProperty property)
        {
            if (property.TenantPersonId?.StartsWith("rent_resident_", StringComparison.Ordinal) == true
                && CityPersonState(property.TenantPersonId) is CityPersonState resident)
            {
                var place = City.GetPlace(property.PlaceId);
                var next = ResidentialPlaces.Where(home => home.Id != property.PlaceId && home.InitiallyVacant
                        && home != Home && !HasResidentialLease(home) && PropertyAt(home.Id) == null
                        && DistrictAccessBlocker(home) == null && !HasResidentialOccupants(home.Id))
                    .OrderByDescending(home => home.StreetId == place.StreetId).FirstOrDefault();
                if (next == null) _cityPeople.Remove(resident.PersonId);
                else
                {
                    bool wasAtFormerHome = resident.LocationPlaceId == property.PlaceId || resident.Route?.TargetPlaceId == property.PlaceId;
                    MoveCityHome(resident, next);
                    if (wasAtFormerHome)
                    {
                        var door = StandOf(next);
                        resident.Route = null;
                        resident.LocationPlaceId = next.Id;
                        resident.X = door.X;
                        resident.Y = door.Y;
                        resident.Task = CityPersonTask.Home;
                        resident.TaskLabel = "在家休息";
                    }
                    if (CityPerson(resident.PersonId) is CityPersonDefinition definition) definition.HomeId = next.Id;
                }
            }
            property.UseMode = "vacant";
            property.TenantPersonId = property.TenantName = null;
            property.WeeklyRentCents = 0;
            property.LeaseEndDay = 0;
            property.RecoverAtLeaseEnd = property.LegacyRentalDiscount = false;
            Say(City.GetPlace(property.PlaceId).Name + "的租约结束，房源已收回。");
        }
    }
}
