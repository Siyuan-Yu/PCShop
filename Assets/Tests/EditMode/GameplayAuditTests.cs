using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Core.Shop;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;
using PCShopSell.Game;
using UnityEngine;

namespace PCShopSell.Tests
{
    // 只采集真实新档的预算与导航报价，不把它当作通关或真人试玩验收。
    public sealed class GameplayAuditTests
    {
        [TestCase(7)] [TestCase(41)] [TestCase(914)]
        [Category("GameplayAudit")]
        public void NewGameOpeningQuotesRemainObservable(int seed)
        {
            var city=CityLayout.FromData(JsonUtility.FromJson<CityLayoutData>(Resources.Load<TextAsset>("Data/city_layout").text));
            using var s=new GameSession(city,(ulong)seed,GameController.StartingCash,
                GameTimestamp.From(new GameDate(0),GameSession.WakeUpMinute),true,false,false,startWithCookingTutorial:true);
            Assert.False(s.HasShop);
            Assert.True(s.OpeningMealTutorialPending);
            var listing=s.Listings.Where(l=>l.Size==ShopSize.Small && l.OpenDay<=0
                && s.DistrictAccessBlocker(city.GetPlace(l.PlaceId))==null).OrderBy(l=>s.LeaseCost(l).Cents).FirstOrDefault();
            Assert.NotNull(listing,"新档须有可立即租用的城郊小店");
            var rows=new System.Collections.Generic.List<string>{"种子\t店型\t铺位\t初始现金\t签约报价\t每周租金\t必需家具原价\t扣租赁和家具后余额（未扣运输、进货、生活费）"};
            foreach(string kind in new[]{"computer","restaurant","clothing","models","secondhand",PetShopCatalog.ShopKind})
            {
                long furniture=GameSession.OpeningFurniture(kind).Sum(id=>FurnitureCatalog.Get(id).Price.Cents);
                long rent=s.LeaseCost(listing).Cents;
                rows.Add($"{seed}\t{kind}\t{listing.PlaceId}\t{GameController.StartingCash.Cents/100d}\t{rent/100d}\t{listing.Rent.Cents/100d}\t{furniture/100d}\t{(s.Ledger.Balance.Cents-rent-furniture)/100d}");
            }
            rows.Add("从新档住处出发的默认交通预计分钟（报价，不是完整实走）");
            foreach(var place in new[]{s.AgencyFor(city.GetPlace(listing.PlaceId)),s.OpeningFurnitureMall,city.GetPlace(listing.PlaceId)})
            {
                Assert.NotNull(place);
                int minutes=s.CityTravelMinutes(place);
                Assert.Less(minutes,1440,"开局必要目的地不能不可达："+place.Id);
                rows.Add(place.Name+"\t"+minutes);
            }
            string root=Environment.GetEnvironmentVariable("PCSHOP_QA_RESULTS")??Path.GetTempPath();
            File.WriteAllLines(Path.Combine(root,$"开局预算-{seed}.tsv"),rows);
        }
    }
}
