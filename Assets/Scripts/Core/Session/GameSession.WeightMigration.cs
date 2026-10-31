using System;
using System.Collections.Generic;
using System.Linq;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        // Conversion is confined to the old save boundary; live storage and calculations use kg.
        private static void MigrateWeightUnits(SaveData data)
        {
            if (data.weightUnitsVersion >= 1) return;
            var seen = new HashSet<SaveStorage>();
            void Storage(SaveStorage s) { if (s != null && seen.Add(s)) s.rice *= 0.5; }
            var h = data.home;
            if (h != null)
            {
                h.riceKg = h.riceJin * 0.5;
                h.packRice *= 0.5; h.boxRice *= 0.5;
                Storage(h.shopLocker); Storage(h.homeTemp); Storage(h.shopTemp);
                Storage(h.homeFridge); Storage(h.mainShopFridge);
                foreach (var courier in h.couriers ?? Array.Empty<SaveCourier>())
                    courier.kilos = (courier.jins ?? Array.Empty<double>()).Select(n => n * 0.5).ToArray();
            }
            foreach (var shop in data.extraShops ?? Array.Empty<SaveShop>())
            { Storage(shop.locker); Storage(shop.temp); Storage(shop.fridge); }
            data.weightUnitsVersion = 1;
        }
    }
}
