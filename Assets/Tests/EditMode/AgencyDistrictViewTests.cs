using System.Linq;
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using PCShopSell.Core.Session;
using PCShopSell.Game;
using PCShopSell.Game.Runtime;
using PCShopSell.Game.UI;
using PCShopSell.Core.World;

namespace PCShopSell.Tests
{
    public sealed class AgencyDistrictViewTests
    {
        private sealed class MapFixture:IDisposable
        {
            public readonly GameSession Session=DistrictProgressionTests.Make();
            public readonly GameController Controller;
            private readonly GameObject _root=new GameObject("AgencyMapFixture"),_world=new GameObject("AgencyMapWorld");
            public MapFixture()
            {
                _root.SetActive(false);Controller=_root.AddComponent<GameController>();
                var world=new StreetWorldBuilder(Session.City,1);
                typeof(StreetWorldBuilder).GetField("_root",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(world,_world.transform);
                foreach(var place in Session.City.Places)
                {
                    var root=new GameObject(place.Id);root.transform.SetParent(_world.transform,false);
                    var point=place.ExplicitEntrance?new Vector3((float)place.EntranceX,0,(float)place.EntranceY):world.NodeWorld(place.NodeId);
                    root.transform.SetPositionAndRotation(point,Quaternion.Euler(0,(float)place.Heading,0));
                    var box=Prims.Box(root.transform,"Building",new Vector3(0,2,(float)place.Depth/2),new Vector3((float)place.Width,4,(float)place.Depth),Mats.Lit(Color.gray),true);
                    box.AddComponent<PlaceMarker>().PlaceId=place.Id;
                    world.Places[place.Id]=new PlaceVisual{Place=place,Root=root,Entrance=point};
                }
                typeof(GameController).GetProperty("Session").SetValue(Controller,Session);
                typeof(GameController).GetProperty("City").SetValue(Controller,Session.City);
                typeof(GameController).GetProperty("World").SetValue(Controller,world);
            }
            public void Dispose(){UnityEngine.Object.DestroyImmediate(_root);UnityEngine.Object.DestroyImmediate(_world);Session.Dispose();}
        }
        private static Camera Preview(AgencyStreetMap view)=>(Camera)typeof(AgencyStreetMap).GetField("_camera",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(view);
        [TestCase("agency")][TestCase("agency_s_services")][TestCase("agency_s_sports")][TestCase("agency_s_night")]
        public void HousingAndCommercialShareTheDistrictFramingButOnlyLocalHomesAreSelectable(string agencyId)
        {
            using(var f=new MapFixture())
            {
                var agency=f.Session.City.GetPlace(agencyId);var units=AgencyStreetMap.DistrictPlaces(f.Session,agency);
                var state=new AgencyStreetMap.ViewState();var a=new GameObject("Commercial",typeof(RectTransform),typeof(RawImage));var b=new GameObject("Residential",typeof(RectTransform),typeof(RawImage));
                try
                {
                    var commercial=a.AddComponent<AgencyStreetMap>();commercial.Initialize(f.Controller,state,units,1120,650);
                    var residential=b.AddComponent<AgencyStreetMap>();residential.Initialize(f.Controller,state,units,1120,650,true);
                    Assert.AreEqual(Preview(commercial).orthographicSize,Preview(residential).orthographicSize,.001f);
                    Assert.Less(Vector3.Distance(Preview(commercial).transform.position,Preview(residential).transform.position),.001f);
                    Assert.True(units.All(p=>f.Session.AgencyServes(agency,p)));
                    foreach(var p in f.Session.City.Places)Assert.AreEqual(p.Kind==PlaceKind.Home&&f.Session.AgencyServes(agency,p),residential.CanSelect(p.Id),p.Id);
                    Assert.False(residential.CanSelect(agencyId));
                    var home=units.First(p=>p.Kind==PlaceKind.Home);var before=f.Session.PlayerPosition;long cash=f.Session.Ledger.Balance.Cents;
                    residential.Focus(home);Assert.That(state.Zoom,Is.InRange(1,8));
                    var at=residential.Project(f.Controller.World.Places[home.Id].Entrance);
                    Assert.AreEqual(560,at.x,1);Assert.That(at.y,Is.InRange(650*.38f,650*.62f));
                    commercial.RenderMap();Assert.AreEqual(Preview(commercial).transform.position,Preview(residential).transform.position);
                    residential.ResetView();Assert.AreEqual(1,state.Zoom);Assert.AreEqual(Vector2.zero,state.Offset);
                    Assert.AreEqual(before,f.Session.PlayerPosition);Assert.AreEqual(cash,f.Session.Ledger.Balance.Cents);
                }
                finally{UnityEngine.Object.DestroyImmediate(a);UnityEngine.Object.DestroyImmediate(b);}
            }
        }
        [Test] public void ActualTabsKeepZoomAndPanAndChangingAgencyResetsToItsOwnDistrict()
        {
            using(var f=new MapFixture())
            {
                var previous=UnityEngine.EventSystems.EventSystem.current;var ui=new GameUi(f.Controller);typeof(GameController).GetProperty("Ui").SetValue(f.Controller,ui);
                void At(string id){var p=f.Session.City.GetPlace(id);typeof(GameSession).GetProperty("CurrentPlace").SetValue(f.Session,p);ui.OnEnteredPlace(p);}
                Button Button(string id)=>ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Button>()).Single(b=>b.name==id);
                AgencyStreetMap Map()=>ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<AgencyStreetMap>()).Single();
                try
                {
                    At("agency");Map().Focus(f.Session.City.GetPlace("player_shop"));
                    var position=Preview(Map()).transform.position;float zoom=Map().Zoom,size=Preview(Map()).orthographicSize;
                    Button("AgencyResidentialTab").onClick.Invoke();
                    Assert.AreEqual(zoom,Map().Zoom);Assert.AreEqual(size,Preview(Map()).orthographicSize,.001f);Assert.AreEqual(position,Preview(Map()).transform.position);
                    Assert.False(Map().CanSelect("player_shop"));Assert.NotNull(Button("ResidentialFocusCurrent"));
                    Button("AgencyCommercialTab").onClick.Invoke();Assert.AreEqual(position,Preview(Map()).transform.position);Assert.AreEqual(zoom,Map().Zoom);
                    ui.CloseAllWindows();At("agency_s_services");Button("AgencyResidentialTab").onClick.Invoke();
                    Assert.AreEqual(1,Map().Zoom);Assert.False(Map().CanSelect("home"));Assert.True(Map().CanSelect("home_tier1"));
                    foreach(var button in ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Button>()).Where(b=>b.name.StartsWith("ResidenceMap_")))
                        Assert.True(f.Session.AgencyServes(f.Session.CurrentPlace,f.Session.City.GetPlace(button.name.Substring("ResidenceMap_".Length))));
                }
                finally
                {
                    ui.CloseAllWindows();foreach(var canvas in ui.Canvases)UnityEngine.Object.DestroyImmediate(canvas.gameObject);
                    if(previous==null&&UnityEngine.EventSystems.EventSystem.current!=null)UnityEngine.Object.DestroyImmediate(UnityEngine.EventSystems.EventSystem.current.gameObject);
                }
            }
        }
        [Test] public void AdministrativeVenueShopDoesNotExpandTheCommunityPropertyOverview()
        {
            var city=CityLayout.FromData(JsonUtility.FromJson<CityLayoutData>(Resources.Load<TextAsset>("Data/city_layout").text));
            var expo=city.GetPlace("life_expo");
            Assert.AreEqual(PlaceKind.Shop,expo.Kind);
            Assert.False(AgencyStreetMap.IsDistrictOverviewPlace(expo));
            Assert.True(AgencyStreetMap.IsDistrictOverviewPlace(city.GetPlace("life_unit_s_market_0")));
            Assert.True(AgencyStreetMap.IsDistrictOverviewPlace(city.GetPlace("agency_s_services")));
        }
        [Test] public void DiagonalStreetFitsActualBuildingsWithoutFramingEmptyWorldBoundsCorners()
        {
            var rotation=Quaternion.Euler(52,45,0);
            var buildings=Enumerable.Range(0,6).Select(i=>new Bounds(new Vector3(i*20,5,i*20),new Vector3(12,10,10))).ToArray();
            var actual=AgencyStreetMap.ProjectedBuildingBounds(buildings,rotation);
            var world=buildings[0];foreach(var b in buildings.Skip(1))world.Encapsulate(b);
            var inflated=AgencyStreetMap.ProjectedBuildingBounds(new[]{world},rotation);
            Assert.Less(actual.size.x,inflated.size.x*.3f);
            foreach(var b in buildings)
            foreach(var corner in AgencyStreetMap.Corners(b))
                Assert.Less(Vector3.Distance(actual.ClosestPoint(Quaternion.Inverse(rotation)*corner),Quaternion.Inverse(rotation)*corner),.001f);
        }
        [Test] public void FramingIsTranslationInvariantAndIncludesRoofHeight()
        {
            var rotation=Quaternion.Euler(52,45,0);var shift=new Vector3(800,0,-600);
            var buildings=new[]{new Bounds(new Vector3(10,9,20),new Vector3(15,18,14)),new Bounds(new Vector3(40,3,50),new Vector3(9,6,10))};
            var original=AgencyStreetMap.ProjectedBuildingBounds(buildings,rotation);
            var moved=AgencyStreetMap.ProjectedBuildingBounds(buildings.Select(b=>new Bounds(b.center+shift,b.size)),rotation);
            Assert.Less(Vector3.Distance(original.size,moved.size),.001f);
            Assert.Less(Vector3.Distance(original.center+Quaternion.Inverse(rotation)*shift,moved.center),.001f);
        }
    }
}
