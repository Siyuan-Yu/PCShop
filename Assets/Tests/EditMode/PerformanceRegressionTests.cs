using System;
using System.Collections.Generic;
using NUnit.Framework;
using PCShopSell.Core.Home;
using PCShopSell.Core.Shop;
using PCShopSell.Core.World;
using PCShopSell.Game.Runtime;
using UnityEngine;

namespace PCShopSell.Tests
{
    public sealed class PerformanceRegressionTests
    {
        [Test]
        public void CachedNearestNodeMatchesExhaustiveSearchAndInvalidatesAfterMapGrowth()
        {
            var city=CityLayout.FromData(GameSessionTests.Layout());
            string Brute(double x,double y)
            {
                string best=null;double distance=double.MaxValue;
                foreach(var n in city.Map.Nodes)
                {
                    double dx=n.X-x*city.DistanceScale,dy=n.Y-y*city.DistanceScale,d=dx*dx+dy*dy;
                    if(d<distance){distance=d;best=n.Id;}
                }
                return best;
            }
            var random=new System.Random(7);
            for(int i=0;i<4500;i++)
            {
                double x=random.NextDouble()*1000-500,y=random.NextDouble()*1000-500;
                string expected=Brute(x,y);
                Assert.AreEqual(expected,city.NearestNode(x,y));
                Assert.AreEqual(expected,city.NearestNode(x,y));
            }
            city.NearestNode(10000,10000);
            city.Map.AddNode("new-nearest",10000*city.DistanceScale,10000*city.DistanceScale);
            Assert.AreEqual("new-nearest",city.NearestNode(10000,10000));
        }

        [Test]
        public void FurnitureDetectsInPlaceMutationAndRoomResizeButRetainsUnchangedModels()
        {
            var root=new GameObject("FurniturePerformanceRegression");
            var view=new RoomFurniture(root.transform);
            var layout=new List<PlacedFurniture>{new PlacedFurniture{Id="stool",X=2,Z=2}};
            try
            {
                Assert.True(view.Sync(layout,RoomSpec.Home));
                var first=view.Entries[0].Go;
                for(int i=0;i<60;i++)Assert.False(view.Sync(layout,RoomSpec.Home));
                Assert.AreSame(first,view.Entries[0].Go);
                layout[0].X=3;Assert.True(view.Sync(layout,RoomSpec.Home));
                Assert.AreEqual(3,view.Entries[0].Go.transform.localPosition.x,.001);
                Assert.True(view.Sync(layout,RoomSpec.ForShop(ShopSize.Medium)));
                Assert.False(view.Sync(layout,RoomSpec.ForShop(ShopSize.Medium)),"等价的新 RoomSpec 不应导致重建");
                layout.Clear();Assert.True(view.Sync(layout,RoomSpec.Home));Assert.IsEmpty(view.Entries);
                view.Destroy();Assert.True(view.Sync(layout,RoomSpec.Home));
            }
            finally{view.Destroy();UnityEngine.Object.DestroyImmediate(root);}
        }
    }
}
