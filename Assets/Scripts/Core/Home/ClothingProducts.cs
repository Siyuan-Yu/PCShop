using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Session;
namespace PCShopSell.Core.Home
{
    public static class ClothingProducts
    {
        public const string Business="服装";
        public static readonly IReadOnlyList<GiftDef> All=Outfits.All.Where(x=>x.Id!=Outfits.Default).Select(x=>
            new GiftDef(x.ProductId,x.Name,x.FictionalBrandId+" · "+x.TierName+" · "+x.OccasionName+" · "+x.ComfortMinC+"—"+x.ComfortMaxC+"℃。"+x.Description,
                x.Price.Yuan,x.WeightKg,Business)).ToArray();
        public static OutfitDef Outfit(string productId)=>productId!=null&&productId.StartsWith("clothing:")?Outfits.Get(productId.Substring(9)):null;
        public static bool IsClothing(string id)=>Outfit(id)!=null;
        public static double SeasonDemandAt(string productId,double temperature)
        {
            var o=Outfit(productId); if(o==null)return 1;
            if(temperature>=o.ComfortMinC&&temperature<=o.ComfortMaxC)return 1.25;
            double distance=Math.Max(o.ComfortMinC-temperature,temperature-o.ComfortMaxC);
            return distance>=6?.65:1;
        }
    }
}
