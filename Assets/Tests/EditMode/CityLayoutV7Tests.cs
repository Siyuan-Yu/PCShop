using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;
using UnityEngine;

namespace PCShopSell.Tests
{
    public sealed class CityLayoutV7Tests
    {
        private static CityLayout City()=>CityLayout.FromData(JsonUtility.FromJson<CityLayoutData>(Resources.Load<TextAsset>("Data/city_layout").text));
        private static GameSession Session(CityLayout city)=>new GameSession(city,123,Money.FromYuan(100000),GameTimestamp.From(new GameDate(5),600));
        [Test] public void ConfirmedPlanHasOneAgencyPerStreetAndNoStarterShopStation()
        {
            var city=City();Assert.AreEqual(7,city.LayoutVersion);Assert.AreEqual(10,city.Streets.Count);
            foreach(var street in city.Streets)Assert.AreEqual(1,city.OfKind(PlaceKind.Agency).Count(p=>p.StreetId==street.id),street.id);
            Assert.AreEqual(8,city.OfKind(PlaceKind.Station).Count());
            Assert.False(city.OfKind(PlaceKind.Station).Any(p=>p.StreetId=="s_start"));
            Assert.GreaterOrEqual(city.Places.Count(p=>p.IsTradable),20);
            foreach(var n in city.Map.Nodes)
            foreach(var edge in city.Map.NeighborsOf(n.Id))
            {
                var other=city.Map.GetNode(edge.To);
                Assert.True(n.X==other.X || n.Y==other.Y);
            }
            foreach(var p in city.Places)Assert.True(Pathfinder.FindPath(city.Map,city.PlayerShop().NodeId,p.NodeId).Found,p.Id);
        }
        [Test] public void ResidentialSceneryHasMultipleRowsAndDoesNotCollapseToFourApartmentBlocks()
        {
            var city=City();
            foreach(var pair in new[]{new {Street="s_oldhomes",Minimum=45},new {Street="s_services",Minimum=25},new {Street="s_sports",Minimum=25},new {Street="s_newtown",Minimum=35}})
            {
                var blocks=city.Blocks.Where(b=>b.street==pair.Street).ToArray();
                Assert.GreaterOrEqual(blocks.Length,pair.Minimum,pair.Street);
                Assert.GreaterOrEqual(blocks.Select(b=>b.width).Distinct().Count(),3,pair.Street);
                Assert.GreaterOrEqual(blocks.Select(b=>b.height).Distinct().Count(),3,pair.Street);
                Assert.GreaterOrEqual(blocks.Select(b=>b.y).Distinct().Count(),5,pair.Street);
            }
            foreach(var block in city.Blocks)
            foreach(var place in city.Places)
            {
                var at=city.ViewPosition(place.NodeId);
                double front=at.Y+place.EntranceY,back=front+(place.EntranceY>=0?1:-1)*place.Depth;
                bool collision=block.x-block.width/2<at.X+place.Width/2 && block.x+block.width/2>at.X-place.Width/2
                    && block.y<System.Math.Max(front,back) && block.y+block.depth>System.Math.Min(front,back);
                Assert.False(collision,pairLabel(block.street,place.Id));
            }
        }
        private static string pairLabel(string street,string place)=>street+" / "+place;
        [Test] public void MetroTravelsToChosenStationRatherThanLastEnumeratedStation()
        {
            var city=City();using(var s=Session(city))
            {
                var save=s.CreateSave();var origin=city.GetPlace("metro_a");
                save.currentPlace=origin.Id;save.playerNode=origin.NodeId;save.cityTrip=null;save.cityPlayerOffNode=false;
                using(var restored=GameSession.Load(city,save))
                {
                    var cash=restored.Ledger.Balance;var now=restored.Clock.Now.TotalMinutes;
                    Assert.True(restored.TakeMetro("metro_luxury"));Assert.AreEqual("metro_luxury",restored.CurrentPlace.Id);
                    Assert.AreEqual(now+GameSession.MetroMinutes,restored.Clock.Now.TotalMinutes);
                    Assert.AreEqual(cash-GameSession.MetroFare,restored.Ledger.Balance);
                    Assert.False(restored.TakeMetro("player_shop"));Assert.False(restored.TakeMetro("metro_luxury"));
                }
            }
        }
        [Test] public void LayoutMigrationDoesNotReplayCashOrDuplicateAssetsAndThenIsIdempotent()
        {
            var city=City();using(var s=Session(city))
            {
                var save=s.CreateSave();save.cityLayoutVersion=0;save.currentPlace="home";save.playerNode=city.GetPlace("home").NodeId;
                var cash=s.Ledger.Balance;int people=s.CityPeople.Count;
                using(var migrated=GameSession.Load(city,save))
                {
                    Assert.AreEqual(cash,migrated.Ledger.Balance);Assert.AreEqual(people,migrated.CityPeople.Count);
                    var current=migrated.CreateSave();Assert.AreEqual(7,current.cityLayoutVersion);
                    using(var again=GameSession.Load(city,current))
                    {Assert.AreEqual(cash,again.Ledger.Balance);Assert.AreEqual(people,again.CityPeople.Count);Assert.AreEqual(migrated.Home.Id,again.Home.Id);}
                }
            }
        }
    }
}
