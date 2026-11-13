using System;
using System.Linq;
using PCShopSell.Core.Home;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Shop;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        public const double WarehouseCapacity = double.MaxValue;
        public static int ShelfLimit(ShopSize size)=>size==ShopSize.Large?20:size==ShopSize.Medium?12:8;
        public static int StaffLimit(ShopSize size)=>size==ShopSize.Large?10:size==ShopSize.Medium?6:4;
        public int ShopShelfLimit=>ShelfLimit(PlayerShop.Size);
        public int ShopStaffLimit=>StaffLimit(PlayerShop.Size);
        private void EnsureWarehouseBox(System.Collections.Generic.List<PlacedFurniture> layout,RoomSpec room,string id)
        {
            if(BoxesIn(layout)>0)return;
            var def=FurnitureCatalog.Get(id);
            for(double x=.3;x<room.Width-.3;x+=.25)
                for(double z=.25;z<room.Depth-.25;z+=.25)
                {
                    var at=RoomPlanner.Snap(room,def,x,z,0);
                    if(RoomPlanner.Check(room,layout,def,at,-1,null)!=null)continue;
                    layout.Add(at);return;
                }
            AddFurniture(id,1);
        }
        private void MigrateWarehouseBoxAccess(SaveData data)
        {
            // One-time recovery for the version that removed box furniture. A player who
            // later removes their final box must not have it silently respawn on every load.
            if(data.warehouseAccessVersion>=1)return;
            EnsureWarehouseBox(_homeLayout,ResidenceRoom,FurnitureCatalog.HomeBox);
            foreach(var shop in _shops.Where(s=>s.Rented&&!IsDedicatedVenue(s.Place)))
                EnsureWarehouseBox(shop.Layout,RoomSpec.ForShop(shop.Place.Size),FurnitureCatalog.ShopBox);
        }
        private static bool IsBulkStorageFood(string id) => IsEdibleGift(id) || RetailProducts.IsFood(Gifts.Get(id));

        private static void MergeStorage(Backpack from,Backpack to,bool includeFood = true)
        {
            foreach (var item in from.GiftLines.ToArray())
            {
                if (!includeFood && IsBulkStorageFood(item.Key)) continue;
                to.AddGift(item.Key, item.Value);
                from.TakeGift(item.Key, item.Value);
            }
            foreach(var item in from.FurnitureLines.ToArray())to.AddFurniture(item.Key,item.Value);
            from.ClearFurniture();
            if (!includeFood) return;
            to.Food.RiceKg+=from.Food.RiceKg;from.Food.RiceKg=0;
            to.Food.Ingredient.AddRange(from.Food.Ingredient);from.Food.Ingredient.Clear();
            to.Food.Packed.AddRange(from.Food.Packed);from.Food.Packed.Clear();
        }
        private void NormalizeWarehouses()
        {
            MergeStorage(HomeTemp,HomeBox);
            foreach(var line in HomeTempParts.Lines().ToArray())HomeBoxParts.ForceAdd(line.Part,line.Boxed,line.Count);
            HomeTempParts.Clear();_homeTempSince=0;
            foreach(var shop in _shops)
            {
                shop.Sim.Stock.Capacity=WarehouseCapacity;
                MergeStorage(shop.Temp,shop.Locker);shop.TempSince=0;
            }
        }
        private string ShelfPlacementBlocker(bool shop,FurnitureDef furniture,int ignore=-1)
        {
            if(!shop||furniture?.Fixture!=FixtureKind.Shelf)return null;
            if(ignore>=0&&ignore<_shopLayout.Count&&FurnitureCatalog.Get(_shopLayout[ignore].Id)?.Fixture==FixtureKind.Shelf)return null;
            int count=_shopLayout.Where((f,index)=>index!=ignore).Count(f=>FurnitureCatalog.Get(f.Id)?.Fixture==FixtureKind.Shelf);
            return count>=ShopShelfLimit?"这间"+SizeName(PlayerShop.Size)+"最多摆 "+ShopShelfLimit+" 个货架，其他家具不限数量。":null;
        }
    }
}
