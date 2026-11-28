using System.Collections.Generic;
using PCShopSell.Core.Home;
using PCShopSell.Core.Shop;
using UnityEngine;

namespace PCShopSell.Game.Runtime
{
    public sealed partial class ShopInteriorView
    {
        private string _visitedAddress,_visitedGoods;
        private bool _visitedVacant;
        // Occupied public shops display stock as scenic models. A vacant property is delivered empty.
        internal void Visit(string address,bool vacant,string goods,float dt,float minute)
        {
            if(_visitedAddress!=address || _visitedVacant!=vacant || _visitedGoods!=goods)
            {
                Hook(null);
                HookShopInteractions(null);
                ReleaseStagedPayer();
                if (_selfCheckoutTerminal != null) _selfCheckoutTerminal.SetActive(false);
                _visitedAddress=address; _visitedVacant=vacant; _visitedGoods=goods;
                var layout=vacant?new List<PlacedFurniture>():FurnitureCatalog.DefaultShopFixtures(Size,"computer");
                if(!vacant && goods!="gpu")
                    foreach(var item in layout) if(FurnitureCatalog.Get(item.Id)?.IsFixture==true &&
                        FurnitureCatalog.Get(item.Id).Fixture==FixtureKind.Shelf) item.Item=goods;
                _decor.Sync(layout,RoomSpec.ForShop(Size));
                RebuildFixtures();
                foreach(var shelf in _shelves)
                {
                    if(shelf.EmptyMark!=null)shelf.EmptyMark.SetActive(false);
                    foreach(var item in shelf.GoodsItems)item.SetActive(true);
                    foreach(var item in shelf.BoxedItems)item.SetActive(true);
                }
                StandAtDoor();
            }
            UpdateStreet(dt,minute);
            // Property tours use the normal powered shop lights while leaving the business closed.
            UpdateOpenLook(true,dt);
            if(vacant) { _doorHang.SetActive(false); _signOpen.SetActive(false); _signClosed.SetActive(false); }
        }
    }
}
