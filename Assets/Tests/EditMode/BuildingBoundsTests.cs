using System.Linq;
using NUnit.Framework;
using UnityEngine;
using PCShopSell.Game.Runtime;
using PCShopSell.Game.UI;

namespace PCShopSell.Tests
{
    public sealed class BuildingBoundsTests
    {
        private static GameObject Box(Transform parent,string name,Vector3 center,Vector3 size)
        {
            var box=GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name=name;box.transform.SetParent(parent,false);
            box.transform.localPosition=center;box.transform.localScale=size;return box;
        }
        private static PlaceVisual Building(Transform parent,string name,float yaw)
        {
            var root=new GameObject("Place");root.transform.SetParent(parent,false);
            root.transform.localPosition=new Vector3(-80,3,125);
            root.transform.localRotation=Quaternion.Euler(0,yaw,0);
            root.transform.localScale=new Vector3(1.4f,1.1f,.8f);
            Box(root.transform,name,new Vector3(0,3,5),new Vector3(12,6,10));
            Box(root.transform,"Roof",new Vector3(0,6.25f,5),new Vector3(13,.5f,11));
            return new PlaceVisual{Root=root,Entrance=root.transform.position};
        }
        [TestCase("Body",0)]
        [TestCase("Building",90)]
        [TestCase("CustomFacade",137)]
        public void ActualGeometryDefinesBoundsRegardlessOfNamesHeadingAndParentTransform(string name,float yaw)
        {
            var parent=new GameObject("BoundsRegression");
            try
            {
                parent.transform.position=new Vector3(140,-2,320);
                parent.transform.rotation=Quaternion.Euler(0,25,0);parent.transform.localScale=new Vector3(1.5f,2,.75f);
                var place=Building(parent.transform,name,yaw);
                var local=place.BuildingBounds(place.Root.transform);
                Assert.That(Vector3.Distance(local.center,new Vector3(0,3.25f,5)),Is.LessThan(.001f));
                Assert.That(Vector3.Distance(local.size,new Vector3(13,6.5f,11)),Is.LessThan(.001f));
                var world=place.BuildingBounds();
                Assert.AreEqual(world,AgencyStreetMap.BuildingBounds(place));
                foreach(var renderer in place.Root.GetComponentsInChildren<MeshRenderer>())
                {
                    var mesh=renderer.GetComponent<MeshFilter>().sharedMesh.bounds;
                    foreach(var point in AgencyStreetMap.Corners(mesh))
                    {
                        var actual=renderer.localToWorldMatrix.MultiplyPoint3x4(point);
                        Assert.That(Vector3.Distance(world.ClosestPoint(actual),actual),Is.LessThan(.001f));
                    }
                }
            }
            finally{Object.DestroyImmediate(parent);}
        }
        [Test]
        public void ExteriorBoundsIgnoreInactiveInteriorAndIncludeTheWholeRoof()
        {
            var parent=new GameObject("ExteriorBoundsRegression");
            try
            {
                var place=Building(parent.transform,"Building",90);
                var exterior=new GameObject("Exterior");exterior.transform.SetParent(place.Root.transform,false);
                foreach(var child in place.Root.transform.Cast<Transform>().Where(t=>t!=exterior.transform).ToArray())child.SetParent(exterior.transform,false);
                place.Exterior=exterior;
                var interior=Box(place.Root.transform,"VenueInterior",new Vector3(200,50,200),Vector3.one*100);
                interior.SetActive(false);exterior.SetActive(false);
                var bounds=place.BuildingBounds(place.Root.transform);
                Assert.That(Vector3.Distance(bounds.center,new Vector3(0,3.25f,5)),Is.LessThan(.001f));
                Assert.That(Vector3.Distance(bounds.size,new Vector3(13,6.5f,11)),Is.LessThan(.001f));
            }
            finally{Object.DestroyImmediate(parent);}
        }
        private static void AssertShellFits(Transform glow,PlaceVisual place)
        {
            var shell=glow.Find("Shell");Assert.IsNotNull(shell);
            foreach(var renderer in place.Root.GetComponentsInChildren<MeshRenderer>(true))
                foreach(var corner in AgencyStreetMap.Corners(renderer.GetComponent<MeshFilter>().sharedMesh.bounds))
                {
                    var point=shell.InverseTransformPoint(renderer.localToWorldMatrix.MultiplyPoint3x4(corner));
                    Assert.LessOrEqual(Mathf.Abs(point.x),.501f);Assert.LessOrEqual(Mathf.Abs(point.y),.501f);Assert.LessOrEqual(Mathf.Abs(point.z),.501f);
                }
            var actual=place.BuildingBounds(place.Root.transform);
            Assert.Less(shell.localScale.x,actual.size.x+1);
            Assert.Less(shell.localScale.z,actual.size.z+1);
        }
        [Test]
        public void BeaconFitsNewModelsFollowsTransformsAndRebindsTheSamePlaceWithoutGrowingMapBounds()
        {
            var parent=new GameObject("BeaconRegression");
            using(var session=GameSessionTests.Make())
            try
            {
                parent.transform.position=new Vector3(150,0,-75);
                parent.transform.rotation=Quaternion.Euler(0,31,0);parent.transform.localScale=new Vector3(2,1.5f,.8f);
                var place=Building(parent.transform,"Building",90);place.Place=session.City.GetPlace("pc");
                var world=new StreetWorldBuilder(session.City,1);world.Places["pc"]=place;
                var before=place.BuildingBounds();
                var beacon=new ShopBeacon(parent.transform);beacon.Update(session,world,"pc",0);
                var glow=parent.transform.Find("MyShopGlow");AssertShellFits(glow,place);
                Assert.AreEqual(before,AgencyStreetMap.BuildingBounds(place),"高亮本身不能被计入建筑范围");
                place.Root.transform.localPosition+=new Vector3(33,4,-22);
                place.Root.transform.localRotation=Quaternion.Euler(0,137,0);place.Root.transform.localScale=new Vector3(.8f,2,1.5f);
                beacon.Update(session,world,"pc",.1f);AssertShellFits(glow,place);
                var replacement=Building(parent.transform,"Facade",-45);replacement.Place=place.Place;
                replacement.Root.transform.Find("Facade").localPosition=new Vector3(0,4.5f,7.5f);
                replacement.Root.transform.Find("Facade").localScale=new Vector3(21,9,15);
                world.Places["pc"]=replacement;
                beacon.Update(session,world,"pc",.1f);AssertShellFits(glow,replacement);
                beacon.Update(session,world,null,0);Assert.IsFalse(beacon.Visible);
            }
            finally{Object.DestroyImmediate(parent);}
        }
    }
}
