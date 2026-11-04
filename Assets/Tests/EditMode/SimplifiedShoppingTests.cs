using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Core.Trade;
using PCShopSell.Core.World;

namespace PCShopSell.Tests
{
    public sealed class SimplifiedShoppingTests
    {
        private static GameSession Make()=>new GameSession(CityLayout.FromData(GameSessionTests.Layout()),7,Money.FromYuan(100000),GameTimestamp.From(new GameDate(0),601),true,true);
        [Test] public void OnlyOneShoppingAppAndLegacyInstallationMapsToIt()
        {
            using(var s=Make())
            {
                Assert.False(PhoneApps.All.Any(a=>a.Id==PhoneApps.Wholesale));
                Assert.True(s.UninstallApp(PhoneApps.Shop));Assert.NotNull(s.AppBlocker(PhoneApps.Wholesale));
                Assert.True(s.InstallApp(PhoneApps.Wholesale));Assert.True(s.HasApp(PhoneApps.Shop));
                Assert.False(s.InstalledApps.Contains(PhoneApps.Wholesale));
            }
        }
        [Test] public void OldAppOnlySaveBecomesShoppingApp()
        {
            using(var s=Make())
            {
                var save=s.CreateSave();save.apps=new[]{PhoneApps.Store,PhoneApps.Contacts,PhoneApps.Wholesale};
                using(var loaded=GameSession.Load(s.City,save))
                {Assert.True(loaded.HasApp(PhoneApps.Shop));Assert.False(loaded.InstalledApps.Contains(PhoneApps.Wholesale));}
            }
        }
        [Test] public void SingleOnlineQuoteAndAutoRestockIgnoreOldVendorSelection()
        {
            using(var s=Make())
            {
                s.RestockStoreId="s_cheap";
                Assert.AreEqual(Online1788.Shopping,s.PickRestockStore(new[]{PartCatalog.Get("cpu0")}));
                var quotes=s.ProcurementSources(PurchaseGoodsKind.Part,"cpu0").Where(q=>q.Method==ProcurementMethod.Online1788).ToArray();
                Assert.AreEqual(1,quotes.Length);Assert.AreEqual("网购",quotes[0].SupplierName);
            }
        }
        [Test] public void OldOrderEntryCreatesSimpleOrderWithDisplayedPriceAndNoVendorDefects()
        {
            using(var s=Make())
            {
                var part=PartCatalog.Get("cpu0");long before=s.Ledger.Balance.Cents;
                long price=(s.OnlinePrice(Online1788.Shopping,part)*2+Online1788.ShippingFee).Cents;
                Assert.True(s.PlaceOnlineOrder("s_cheap",new[]{(part,2)}));
                var order=s.Online.Pending.Single();Assert.AreEqual(Online1788.Shopping.Id,order.StoreId);
                Assert.AreEqual(price,before-s.Ledger.Balance.Cents);Assert.AreEqual(price,order.TotalCents);
                int stock=s.PlayerShop.Stock.CountAll(part);
                typeof(GameSession).GetMethod("DeliverOnlineOrders",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(s,new object[]{GameTimestamp.From(new GameDate(1),600)});
                Assert.AreEqual(stock+2,s.PlayerShop.Stock.CountAll(part));Assert.Zero(s.Online.Pending.Count);
            }
        }
        [Test] public void PreviouslyPaidOrdersKeepTheirVendorAndArrivalOnLoad()
        {
            using(var s=Make())
            {
                var old=new OnlineOrder{Id=23,StoreId="s_star",TotalCents=12345,DeliverAtMinute=2040,ShopId=s.MainShop.Place.Id};
                old.Lines.Add(new OnlineOrderLine{PartId="cpu0",Count=2});s.Online.AddPending(old);
                using(var loaded=GameSession.Load(s.City,s.CreateSave()))
                {var restored=loaded.Online.Pending.Single();Assert.AreEqual("s_star",restored.StoreId);Assert.AreEqual(12345,restored.TotalCents);Assert.AreEqual(2040,restored.DeliverAtMinute);}
            }
        }
    }
}
