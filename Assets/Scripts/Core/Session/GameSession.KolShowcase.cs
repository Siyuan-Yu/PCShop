using System;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Home;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        public string KolShowcaseKind(KolRoomStatus room)
        {
            var brand = _brands.Brands.FirstOrDefault(b => b.Id == room?.BrandId);
            return brand?.Kind ?? room?.Session?.RivalKind ?? KolMarket.Get(room?.PersonId)?.Kind ?? "computer";
        }

        // Brand broadcasts show actual stock. Public broadcasts use catalogue teaching samples.
        // Selecting a visual never reserves, sells or creates inventory.
        public string[] KolShowcaseProducts(KolRoomStatus room)
        {
            if (room == null) return Array.Empty<string>();
            if (room.BrandId != null)
            {
                var brand = _brands.Brands.FirstOrDefault(b => b.Id == room.BrandId);
                if (brand == null) return Array.Empty<string>();
                return brand.ShopIds.Where(id => ShopAt(id)?.Rented == true && ShopAt(id).RetailKind == brand.Kind)
                    .SelectMany(BrandProducts).Distinct().OrderBy(id => id, StringComparer.Ordinal).ToArray();
            }
            string kind = KolShowcaseKind(room);
            if (kind == "models") return ModelProducts.Regular.Select(p => p.Id).ToArray();
            if (kind == "clothing") return ClothingProducts.All.Select(p => p.Id).ToArray();
            return PartCatalog.All.Where(p => p.Tier == 0).Select(p => p.Id).Take(6).ToArray();
        }

        public string KolShowcaseProduct(KolRoomStatus room)
        {
            var goods = KolShowcaseProducts(room);
            if (goods.Length == 0) return null;
            long elapsed = Math.Max(0, Clock.Now.TotalMinutes - (room.Session?.StartedAt ?? Clock.Now.TotalMinutes));
            return goods[(int)(elapsed / 20 % goods.Length)];
        }
    }

}
