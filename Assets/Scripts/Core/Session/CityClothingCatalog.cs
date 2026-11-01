using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;

namespace PCShopSell.Core.Session
{
    public sealed class OutfitDef
    {
        public string Id { get; }
        public string OutfitStyleId => Id;
        public string ProductId => "clothing:" + Id;
        public string Name { get; }
        public Money Price { get; }
        public Money RetailBasePrice => Price;
        public int CharmBonus { get; }
        public string Description { get; }
        public int Tier { get; }
        public string FictionalBrandId { get; }
        public string OccasionTags { get; }
        public string SeasonTags { get; }
        public double ComfortMinC { get; }
        public double ComfortMaxC { get; }
        public string VisualPreset { get; }
        public string ColorHex { get; }
        public double WeightKg { get; }
        public OutfitDef(string id, string name, long priceYuan, int charmBonus, string description,
            int tier = 0, string occasion = "daily", double minC = 23, double maxC = 32,
            string visual = "short", string color = "BF7753", string seasons = "summer")
        {
            Id=id; Name=name; Price=Money.FromYuan(priceYuan); CharmBonus=Math.Min(9,Math.Max(0,charmBonus)); Description=description;
            Tier=tier; OccasionTags=occasion; ComfortMinC=minC; ComfortMaxC=maxC; VisualPreset=visual; ColorHex=color; SeasonTags=seasons;
            FictionalBrandId=new[]{"巷日","远迹","序章","岚序"}[Math.Max(0,Math.Min(3,tier))];
            WeightKg=visual=="winter"?1.8:visual=="short"?.35:visual=="formal"?1.1:.75;
        }
        public bool IsOccasion(string occasion) => OccasionTags.Split(',').Contains(occasion);
        public string TierName => new[]{"平价","品质","精品","奢侈"}[Tier];
        public string OccasionName => string.Join(" / ",OccasionTags.Split(',').Select(x=>x=="business"?"商务":x=="sport"?"运动":"日常"));
    }
    public static class Outfits
    {
        public const string Default="tee";
        public static readonly IReadOnlyList<OutfitDef> All=new[]
        {
            new OutfitDef("tee","地摊 T 恤",0,0,"洗得发白，但是舒服。"),
            new OutfitDef("cotton_short","巷日·棉风短袖套装",99,1,"轻棉短袖和及膝短裤。",0,"daily",23,32,"short","8CB5AE"),
            new OutfitDef("sport_basic","巷日·晨练运动套装",169,1,"透气短袖与宽松运动短裤。",0,"sport,daily",22,32,"sport","6B98B0"),
            new OutfitDef("shirt","干净衬衫",280,2,"至少看起来像个正经老板。",0,"business,daily",18,28,"long","EAECEF","spring,autumn"),
            new OutfitDef("knit_basic","巷日·细纹长袖套装",239,1,"柔软长袖与直筒长裤。",0,"daily",16,26,"long","C4B39B","spring,autumn"),
            new OutfitDef("winter_basic","巷日·暖棉厚装",199,1,"朴素厚棉外套与保暖长裤，可靠保暖。",0,"daily",0,17,"winter","7C8975","winter"),
            new OutfitDef("winter_active","巷日·冬行厚装",299,2,"厚外套加防风领，日常和轻运动都方便。",0,"sport,daily",-2,17,"winter","BB795F","winter"),
            new OutfitDef("jacket","休闲夹克",700,4,"出门谈事不丢人。",1,"daily,business",12,24,"jacket","705037","spring,autumn"),
            new OutfitDef("trail_light","远迹·轻途运动装",580,3,"长袖运动外套，留足活动空间。",1,"sport,daily",16,28,"sport_long","708C9D","spring,autumn"),
            new OutfitDef("linen_day","远迹·晴昼亚麻套装",620,3,"夏日短袖，不因价格高就保暖。",1,"daily",23,32,"short","D4C8AF"),
            new OutfitDef("shirt_quality","远迹·青石衬衫套装",890,4,"适合普通商务拜访的长袖。",1,"business,daily",18,28,"long","A3B6C5","spring,autumn"),
            new OutfitDef("coat_quality","远迹·山风厚装",1199,4,"厚棉填充，实用冬装。",1,"daily",-2,16,"winter","54726D","winter"),
            new OutfitDef("formal_quality","远迹·会面西装",1490,5,"低调正式两件套。",1,"business",16,26,"formal","596879","spring,autumn"),
            new OutfitDef("suit","合身西装",1800,7,"进了档口，老板都多看你两眼。",2,"business",16,26,"formal","2B3042","spring,autumn"),
            new OutfitDef("studio_short","序章·留白短袖套装",1680,5,"原创拼色轮廓，仍只适合温暖天气。",2,"daily",23,32,"short","B6A6BD"),
            new OutfitDef("studio_sport","序章·弧线运动装",2100,5,"轻量长袖和运动长裤。",2,"sport,daily",17,28,"sport_long","78928F","spring,autumn"),
            new OutfitDef("studio_jacket","序章·城市夹克",2600,6,"利落夹克与宽松裤装。",2,"daily,business",12,24,"jacket","8B6E78","spring,autumn"),
            new OutfitDef("studio_winter","序章·雪原厚装",3200,6,"厚冬外套，保暖区间与平价冬装相近。",2,"daily",-2,17,"winter","BABCB6","winter"),
            new OutfitDef("studio_formal","序章·夜航礼服套装",4800,8,"正式晚间聚会使用，非严寒户外装。",2,"business",17,27,"formal","394756","spring,autumn"),
            new OutfitDef("lux_short","岚序·云隙短袖套装",5600,7,"手感与剪裁精细的夏装，不额外保暖。",3,"daily",23,32,"short","CFBFAF"),
            new OutfitDef("lux_shirt","岚序·纸庭长袖套装",6800,7,"原创素色衬衫，没有真实品牌图案。",3,"business,daily",18,28,"long","C9D1D3","spring,autumn"),
            new OutfitDef("lux_sport","岚序·轻峰运动装",8200,7,"轻便活动轮廓，不替代厚冬装。",3,"sport,daily",17,28,"sport_long","637C86","spring,autumn"),
            new OutfitDef("lux_jacket","岚序·暮线夹克",12000,8,"都市夹克，价格代表工艺而非恒温能力。",3,"daily,business",12,24,"jacket","6B5E62","spring,autumn"),
            new OutfitDef("lux_winter","岚序·北岸厚装",18000,8,"厚外套与保暖长裤，冬日出门使用。",3,"daily",-2,17,"winter","697684","winter"),
            new OutfitDef("lux_formal","岚序·星序西装",28000,9,"正式套装；衣着印象不替代履约信任。",3,"business",16,26,"formal","303747","spring,autumn"),
        };
        public static OutfitDef Get(string id)=>All.FirstOrDefault(x=>x.Id==id);
    }
}
