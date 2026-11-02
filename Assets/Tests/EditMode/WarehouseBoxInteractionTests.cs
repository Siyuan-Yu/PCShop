using System;
using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Game;
using PCShopSell.Game.Runtime;
using PCShopSell.Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Tests
{
    public sealed class WarehouseBoxInteractionTests
    {
        private static GameSession Make()
        {
            var s=GameSessionTests.Make(100000);
            s.EventsEnabled=false;s.PlayerShop.Settings.BaseCustomersPerHour=0;
            PackTests.EmptyBag(s);GameSessionTests.Teleport(s,"flat");return s;
        }
        private static GameSession Reload(GameSession s)=>GameSession.Load(s.City,JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave())));

        [Test]
        public void RemovingLastBoxHidesAccessWithoutDeletingContentsOrRespawningOnLoad()
        {
            using(var s=Make())
            {
                Assert.AreEqual(1,s.HomeBoxes);Assert.True(s.HasWarehouseHere);
                Assert.AreEqual(HomeRoomView.Storage,HomeRoomView.RoleId(FurnitureCatalog.Get(FurnitureCatalog.HomeBox).Role));
                s.Backpack.AddGift(Gifts.Cig,5000);Assert.True(s.StoreInBox(Gifts.Cig,5000));
                int index=s.HomeLayout.ToList().FindIndex(p=>FurnitureCatalog.Get(p.Id)?.Role==FurnitureRole.Storage);
                var original=s.HomeLayout[index];
                Assert.True(s.StoreFurniture(false,index));Assert.False(s.HasWarehouseHere);
                Assert.IsNull(s.StorageName);Assert.False(s.TakeFromBox(Gifts.Cig));
                Assert.False(s.StoreBackpackInWarehouse());
                Assert.AreEqual(5000,s.HomeBoxCount(Gifts.Cig));
                using(var loaded=Reload(s))
                {
                    Assert.AreEqual(0,loaded.HomeBoxes,"玩家收走的纸箱不能在读档时复活");
                    Assert.AreEqual(5000,loaded.HomeBoxCount(Gifts.Cig));
                    Assert.True(loaded.PlaceFromStock(false,original.Id,original));
                    Assert.True(loaded.HasWarehouseHere);Assert.True(loaded.TakeFromBox(Gifts.Cig));
                    Assert.AreEqual(4999,loaded.HomeBoxCount(Gifts.Cig));
                }
            }
        }

        [Test]
        public void LegacyNoBoxWarehouseGetsOneAccessPointOnceAndPreservesAllGoods()
        {
            using(var s=Make())
            {
                s.HomeBox.AddGift(Gifts.Cig,3000);s.MainShop.Locker.AddGift(Gifts.Cig,2000);
                var save=s.CreateSave();
                var home=s.HomeLayout.ToList();
                foreach(var i in home.Select((p,n)=>new{p,n}).Where(x=>FurnitureCatalog.Get(x.p.Id)?.Role==FurnitureRole.Storage).OrderByDescending(x=>x.n))
                    Assert.True(s.StoreFurniture(false,i.n));
                s.MainShop.Layout.RemoveAll(p=>FurnitureCatalog.Get(p.Id)?.Role==FurnitureRole.Storage);
                save=s.CreateSave();save.warehouseAccessVersion=0;
                using(var loaded=GameSession.Load(s.City,save))
                {
                    Assert.AreEqual(1,loaded.HomeBoxes);Assert.AreEqual(1,loaded.ShopBoxes);
                    Assert.AreEqual(3000,loaded.HomeBoxCount(Gifts.Cig));Assert.AreEqual(2000,loaded.ShopLocker.GiftCount(Gifts.Cig));
                    using(var again=Reload(loaded)){Assert.AreEqual(1,again.HomeBoxes);Assert.AreEqual(1,again.ShopBoxes);}
                }
            }
        }

        [Test]
        public void BulkStoreAndStoreGoodsTakeOutPreserveBatchesAndCarryLimit()
        {
            using(var s=Make())
            {
                s.Backpack.Food.Packed.Add(new FoodBatch{Id="rice",Name="白米饭",Count=2,ExpireDay=80,Chilled=true,ChilledDays=4});
                s.Cargo.ForceAdd(PartCatalog.Get("cpu0"),true,2);s.Backpack.AddGift(Gifts.Cig,3);
                Assert.True(s.StoreBackpackInWarehouse());Assert.True(s.Backpack.IsEmpty);Assert.True(s.Cargo.IsEmpty);
                Assert.AreEqual(2,s.HomeBoxParts.CountAll(PartCatalog.Get("cpu0")));
                var batch=s.HomeBox.Food.Packed.Single();Assert.AreEqual(80,batch.ExpireDay);Assert.True(batch.Chilled);Assert.AreEqual(4,batch.ChilledDays);
                GameSessionTests.Teleport(s,"pc");Assert.AreEqual(1,s.ShopBoxes);
                var clothing=Gifts.All.First(g=>ClothingProducts.IsClothing(g.Id));
                s.PlayerShop.Stock.ForceAddGoods(clothing.Id,1000);
                Assert.False(s.TakeShopGoodsIntoBag(clothing.Id,1000));Assert.AreEqual(1000,s.PlayerShop.Stock.GoodsCount(clothing.Id));
                Assert.True(s.TakeShopGoodsIntoBag(clothing.Id));Assert.AreEqual(1,s.Backpack.GiftCount(clothing.Id));
                Assert.AreEqual(999,s.PlayerShop.Stock.GoodsCount(clothing.Id));
                Assert.True(s.StoreBackpackInWarehouse());Assert.AreEqual(1000,s.PlayerShop.Stock.GoodsCount(clothing.Id),"店内一键转存继续按原规则加入商品库存");
                Assert.AreEqual(3,s.HomeBoxCount(Gifts.Cig),"家和店使用不同后仓");
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void BAndBoxInteractionUseSameFixedWindowWithBackpackAboveWarehouse(bool shop)
        {
            using(var s=Make())
            {
                if(shop)GameSessionTests.Teleport(s,"pc");
                var root=new GameObject("WarehouseBoxUiRegression");root.SetActive(false);
                var controller=root.AddComponent<GameController>();
                typeof(GameController).GetProperty("Session").SetValue(controller,s);
                typeof(GameController).GetProperty("City").SetValue(controller,s.City);
                var existing=UnityEngine.EventSystems.EventSystem.current;var ui=new GameUi(controller);
                Transform Find(string name)=>ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Transform>()).Single(t=>t.name==name);
                try
                {
                    ui.ToggleBackpack();
                    var bag=Find("WarehouseBackpackGrid");var stored=Find("WarehouseStoredGrid");
                    Assert.Less(bag.GetSiblingIndex(),stored.GetSiblingIndex());Assert.AreEqual(bag.parent,stored.parent);
                    Assert.IsNull(bag.parent.GetComponent<ContentSizeFitter>());
                    Assert.AreEqual(2,Find("Window_背包与后仓").GetComponentsInChildren<ScrollRect>().Length);
                    Assert.False(ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Button>()).Any(b=>b.name=="WarehouseAccess"));
                    var other=ui.OpenWindow("临时操作",400,300,w=>{});
                    ui.OpenBackpackPanel();
                    Assert.Greater(other.Root.GetSiblingIndex(),Find("Window_背包与后仓").GetSiblingIndex(),"背包不能遮住更上层的窗口");
                    Assert.True(ui.CloseTopWindow());Assert.True(other.Closed);
                    ui.ToggleBackpack();Assert.False(ui.AnyModalOpen);
                    if(shop)ui.OpenShopLockerWindow();else ui.OpenStorageWindow();
                    Assert.NotNull(Find("WarehouseBackpackGrid"));Assert.NotNull(Find("WarehouseStoredGrid"));
                    ui.ToggleBackpack();Assert.False(ui.AnyModalOpen,"F打开后也能用B收起");
                    if(shop)s.CurrentShop.Layout.RemoveAll(p=>FurnitureCatalog.Get(p.Id)?.Role==FurnitureRole.Storage);
                    else
                    {
                        int index=s.HomeLayout.ToList().FindIndex(p=>FurnitureCatalog.Get(p.Id)?.Role==FurnitureRole.Storage);
                        Assert.True(s.StoreFurniture(false,index));
                    }
                    ui.ToggleBackpack();
                    Assert.False(ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Transform>()).Any(t=>t.name=="WarehouseStoredGrid"));
                    Assert.NotNull(Find("Window_背包"));
                }
                finally
                {
                    ui.CloseAllWindows();foreach(var canvas in ui.Canvases)UnityEngine.Object.DestroyImmediate(canvas.gameObject);
                    UnityEngine.Object.DestroyImmediate(root);
                    if(existing==null&&UnityEngine.EventSystems.EventSystem.current!=null)UnityEngine.Object.DestroyImmediate(UnityEngine.EventSystems.EventSystem.current.gameObject);
                }
            }
        }
    }
}
