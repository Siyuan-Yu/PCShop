using System;
using System.Linq;
using PCShopSell.Core.Home;

namespace PCShopSell.Core.Session
{
    public enum AmusementRide { None, FerrisWheel, RollerCoaster, Carousel, RingToss }

    public sealed partial class GameSession
    {
        private string _gardenPartyActivityId;
        public const int RingsPerRound = 3;
        public static AmusementRide RideAt(CityActivity activity) =>
            RideFor(string.IsNullOrEmpty(activity?.ServiceDefinitionId) ? activity?.DefinitionId : activity.ServiceDefinitionId);

        public static AmusementRide RideFor(string serviceId) => serviceId switch
        {
            "community_ferris" or "garden_ferris" => AmusementRide.FerrisWheel,
            "garden_coaster" => AmusementRide.RollerCoaster,
            "garden_carousel" => AmusementRide.Carousel,
            "garden_ringtoss" => AmusementRide.RingToss,
            _ => AmusementRide.None
        };

        public string[] GardenParty => CurrentPlace?.Id == CityLifeCatalog.GardenAmusementVenue
            ? _cityActivities.FirstOrDefault(a => a.Id == _gardenPartyActivityId)?.Participants.Select(p => p.PersonId).ToArray()
                ?? Array.Empty<string>()
            : Array.Empty<string>();

        private void RememberGardenParty(CityActivity activity, CityActivityPhase phase)
        {
            if (phase != CityActivityPhase.Completed || activity.VenueId != CityLifeCatalog.GardenAmusementVenue
                || CurrentPlace?.Id != activity.VenueId || RideAt(activity) == AmusementRide.None) return;
            _gardenPartyActivityId = activity.Participants.Length == 0 ? null : activity.Id;
            foreach (var participant in activity.Participants)
            {
                var person = CityPersonState(participant.PersonId);
                if (person == null || person.ActivityId != null || person.LocationPlaceId != activity.VenueId) continue;
                person.Task = CityPersonTask.Leisure;
                person.TaskLabel = "在游乐园等同行者";
            }
        }

        public static int RingThrows(CityActivity activity) => activity.Choices.Count(IsRingThrow);
        public static int RingHits(CityActivity activity) => activity.Choices.Count(choice => IsRingThrow(choice) && choice.EndsWith(":hit", StringComparison.Ordinal));
        private static bool IsRingThrow(string choice) => choice.StartsWith("ring:", StringComparison.Ordinal);

        public bool? ThrowParkRing(string activityId, double aim, double power)
        {
            var round = ActiveRingRound(activityId);
            if (round == null || RingThrows(round) % (round.Participants.Length + 1) != 0
                || !double.IsFinite(aim) || !double.IsFinite(power)) return null;
            return RecordParkRing(round, aim, power);
        }

        public (bool Hit, double Aim, double Power)? ThrowCompanionParkRing(string activityId)
        {
            var round = ActiveRingRound(activityId);
            if (round == null) return null;
            int priorThrows = RingThrows(round);
            int turn = priorThrows % (round.Participants.Length + 1);
            if (turn == 0) return null;
            string person = round.Participants[turn - 1].PersonId;
            double spread = .34 - CityPersonAttributes(person).Dexterity * .0024;
            double aim = .58 * (priorThrows % 3 - 1) + Random.Range(-spread, spread);
            double power = .60 + Random.Range(-spread, spread);
            return (RecordParkRing(round, aim, power), aim, power);
        }

        private CityActivity ActiveRingRound(string id)
        {
            var round = RunningCityActivity;
            return round != null && round.Id == id && CurrentPlace?.Id == round.VenueId && RideAt(round) == AmusementRide.RingToss
                && RingThrows(round) < RingsPerRound ? round : null;
        }

        private static bool RecordParkRing(CityActivity round, double aim, double power)
        {
            double nearestBottle = Math.Min(Math.Abs(aim), Math.Min(Math.Abs(aim + .58), Math.Abs(aim - .58)));
            bool hit = nearestBottle <= .13 && Math.Abs(power - .60) <= .18;
            round.Choices = round.Choices.Append($"ring:{RingThrows(round)}:{(hit ? "hit" : "miss")}").ToArray();
            return hit;
        }

        private void SettleAmusementPrize(CityActivity activity)
        {
            if (RideAt(activity) != AmusementRide.RingToss) return;
            int hits = RingHits(activity);
            activity.EventSettlementNote = $"投圈命中 {hits}/{RingsPerRound}。";
            if (hits == 0) return;
            string prizeId = hits >= 2 ? "model:garden:mushroom" : Gifts.Snack;
            var prize = Gifts.Get(prizeId);
            if (prize == null) return;
            if (CanCarry(prize.WeightKg))
            {
                Backpack.AddGift(prizeId, 1);
                ApplyCargoLimit();
                activity.EventSettlementNote += $"「{prize.Name}」已放入背包。";
            }
            else
            {
                HomeBox.AddGift(prizeId, 1);
                activity.EventSettlementNote += $"「{prize.Name}」已送到家中后仓。";
            }
        }
    }

    public sealed partial class SaveData { public string gardenPartyActivityId; }
}
