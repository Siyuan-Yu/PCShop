#if UNITY_EDITOR
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Shop;
namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        private void PrepareRacingAcceptance()
        {
            DebugAddCash(5000000);_district.UnlockedTier=3;DebugAddReputation(50);
            foreach(string model in new[]{"e_city","e_race","t_sport","v_basic","c_su7","c_718"}){var m=MotorCatalog.Get(model);_owned.Add(m.Kind);_garage.Add(NewMotor(model));}
            _raceRecords.Add(new RaceRecord{Key="race_0:0:race",Wins=6,Starts=6,BestSeconds=120});
            foreach(var t in RaceTracks.All)Meet(t.Npc,30);
            var place=City.Places.First(p=>p.Id=="life_unit_s_market_2");
            CurrentPlace=City.GetPlace("agency_s_services");_vacant.Add(place.Id);
            _listings.RemoveAll(l=>l.PlaceId==place.Id);AddListing(place,Clock.Today.TotalDays,Clock.Today.TotalDays+45,Money.FromYuan(1800));
            if(!OpenVenture(place.Id,"cars"))throw new System.InvalidOperationException("赛车验收车行签约失败");
            Leave();ArriveAtNode(City.GetPlace("race_0").NodeId);CurrentPlace=City.GetPlace("race_0");
            Player.Health=100;Player.Energy=90;Flags.Add("debug:racing-acceptance");
        }
    }
}
#endif
