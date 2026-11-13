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
            Flags.Add(RacingAccessFlag);
            Flags.Add(RacingIntroShownFlag);
            DebugAddCash(5000000);_district.UnlockedTier=3;DebugAddReputation(50);
            foreach(var g in RegionalGarages.All)_regionalGarages.Add(new RegionalGarageLease{PlaceId=g.Id,OwnedSpaces=6,NextRentDay=-1});
            foreach(string model in new[]{"e_city","v_basic","c_su7"})PlaceAcquiredMotor(NewMotor(model),true,null);
            foreach(string model in new[]{"e_race","t_sport","c_718"})PlaceAcquiredMotor(NewMotor(model),false,"garage_0");
            PlaceAcquiredMotor(NewMotor("e_turn"),false,"garage_1");
            PlaceAcquiredMotor(NewMotor("t_sport"),false,"garage_2");
            PlaceAcquiredMotor(NewMotor("c_921"),false,"garage_3");
            _raceRecords.Add(new RaceRecord{Key="race_0:0:race",Wins=6,Starts=6,BestSeconds=120});
            foreach(var t in RaceTracks.All)Meet(t.Npc,30);
            var place=City.Places.First(p=>p.Id=="life_unit_s_market_2");
            CurrentPlace=City.GetPlace("agency_s_services");_vacant.Add(place.Id);
            _listings.RemoveAll(l=>l.PlaceId==place.Id);AddListing(place,Clock.Today.TotalDays,Clock.Today.TotalDays+45);
            if(!OpenVenture(place.Id,"cars"))throw new System.InvalidOperationException("赛车验收车行签约失败");
            Leave();ArriveAtNode(City.GetPlace("race_0").NodeId);CurrentPlace=City.GetPlace("race_0");
            Player.Health=100;Player.Energy=90;Flags.Add("debug:racing-acceptance");
        }
    }
}
#endif
