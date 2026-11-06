using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;
using PCShopSell.Game;
using PCShopSell.Game.Runtime;
using PCShopSell.Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Tests
{
    public sealed class BrandShowcaseArtTests
    {
        private static GameSession Make() => GameSession.CreateCityLifePreset(CityLayout.FromData(
            JsonUtility.FromJson<CityLayoutData>(Resources.Load<TextAsset>("Data/city_layout").text)), "quarter_expo");

        [Test] public void EveryMarketHostAndEveryExhibitableProductHasAnIndependentLayer()
        {
            var hosts = KolMarket.All.Select(k => BrandShowcaseArt.Host(k.Id)).ToArray();
            Assert.True(hosts.All(h => h != null));
            Assert.AreEqual(KolMarket.All.Length, hosts.Distinct().Count());
            foreach (string id in PartCatalog.All.Select(p => p.Id).Concat(ModelProducts.All.Select(p => p.Id))
                .Concat(ClothingProducts.All.Select(p => p.Id)))
            {
                var art = BrandShowcaseArt.Product(id);
                Assert.NotNull(art.Texture, id);
                Assert.Greater(art.Uv.width, 0, id); Assert.Greater(art.Uv.height, 0, id);
                Assert.GreaterOrEqual(art.Uv.xMin, 0, id); Assert.GreaterOrEqual(art.Uv.yMin, 0, id);
                Assert.LessOrEqual(art.Uv.xMax, 1.0001f, id); Assert.LessOrEqual(art.Uv.yMax, 1.0001f, id);
            }
            Assert.AreEqual(12, ModelProducts.All.Select(p => BrandShowcaseArt.Product(p.Id).Uv).Distinct().Count());
            Assert.AreNotEqual(BrandShowcaseArt.Product("clothing:shirt").Uv, BrandShowcaseArt.Product("clothing:knit_basic").Uv);
            Assert.AreNotEqual(BrandShowcaseArt.Product("clothing:winter_basic").Tint, BrandShowcaseArt.Product("clothing:winter_active").Tint);
            Assert.IsNull(BrandShowcaseArt.Product("unknown").Texture);
        }

        [Test] public void BrandShowcaseUsesOnlyLinkedRealStockAndDoesNotTakeGoods()
        {
            using (var s = Make())
            {
                var brand = ((BrandBusinessState)typeof(GameSession).GetField("_brands", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(s)).Brands.Single(b => b.Id == "acceptance-expo");
                var room = new KolRoomStatus { PersonId = "life_designer", BrandId = brand.Id,
                    Session = new KolLiveSession { StartedAt = s.Clock.Now.TotalMinutes } };
                Assert.AreEqual(brand.Kind, s.KolShowcaseKind(room));
                var expected = brand.ShopIds.SelectMany(s.BrandProducts).Distinct().OrderBy(id => id, StringComparer.Ordinal).ToArray();
                CollectionAssert.AreEqual(expected, s.KolShowcaseProducts(room));
                Assert.IsNotEmpty(expected);
                int before = s.MainShop.Sim.Stock.Count(PartCatalog.Get(expected[0]), true);
                Assert.AreEqual(expected[0], s.KolShowcaseProduct(room));
                room.Session.StartedAt -= 20;
                Assert.AreEqual(expected[1 % expected.Length], s.KolShowcaseProduct(room));
                Assert.AreEqual(before, s.MainShop.Sim.Stock.Count(PartCatalog.Get(expected[0]), true));
                brand.ShopIds = Array.Empty<string>();
                Assert.IsEmpty(s.KolShowcaseProducts(room));
                Assert.IsNull(s.KolShowcaseProduct(room));
                room.BrandId = "missing-brand";
                Assert.IsNull(s.KolShowcaseProduct(room));
            }
        }

        [Test] public void OtherHostsUseTheExistingStudioWithTheirOwnCutoutAndSample()
        {
            using (var s = Make())
            {
                var root = new GameObject("ShowcaseUiTest"); root.SetActive(false);
                var ctrl = root.AddComponent<GameController>();
                typeof(GameController).GetProperty("Session").SetValue(ctrl, s);
                typeof(GameController).GetProperty("City").SetValue(ctrl, s.City);
                var previous = UnityEngine.EventSystems.EventSystem.current;
                var ui = new GameUi(ctrl);
                var stage = new GameObject("TestStage", typeof(RectTransform));
                try
                {
                    var room = new KolRoomStatus { PersonId = "qiao" };
                    typeof(GameUi).GetMethod("BuildKolStage", BindingFlags.NonPublic | BindingFlags.Instance)
                        .Invoke(ui, new object[] { (RectTransform)stage.transform, KolMarket.Get("qiao"), room, 2 });
                    var images = stage.GetComponentsInChildren<RawImage>();
                    Assert.AreSame(Resources.Load<Texture2D>("KolStudio/computer_studio"), images.Single(i => i.name == "KolStudioArt").texture);
                    Assert.AreSame(BrandShowcaseArt.Host("qiao"), images.Single(i => i.name == "KolPresenter_qiao").texture);
                    Assert.NotNull(images.Single(i => i.name.StartsWith("KolProduct_")).texture);
                    Assert.False(images.Any(i => i.name == "KolFaceCamPortrait" || i.name == "KolCategoryDemo"));
                    Assert.False(s.Time.IsHeld);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(stage);
                    foreach (var canvas in ui.Canvases) if (canvas != null) UnityEngine.Object.DestroyImmediate(canvas.gameObject);
                    if (previous == null && UnityEngine.EventSystems.EventSystem.current != null)
                        UnityEngine.Object.DestroyImmediate(UnityEngine.EventSystems.EventSystem.current.gameObject);
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        [Test] public void ExpoScreensUseBookedGoodsAndOnlyArrivedPresentersWithoutRebuildingTheHall()
        {
            using (var s = Make())
            {
                Assert.True(McnSigningTest.Sign(s,"acceptance-expo", "qiao"));
                Assert.True(s.BookQuarterlyExpo("acceptance-expo", s.MainShop.Place.Id, 0, new[] { "cpu0" }, new[] { "qiao" }));
                var exhibit = s.BrandBusiness.Exhibits.Single();
                s.Clock.Restore(new GameDate(exhibit.Day), 540);
                typeof(GameSession).GetProperty("CurrentPlace").SetValue(s, s.City.GetPlace("life_expo"));
                var person = s.CityPersonState("qiao");
                person.LocationPlaceId = "home"; person.Route = null; person.ActivityId = null;
                var root = new GameObject("ExpoArtworkTest");
                var view = root.AddComponent<QuarterlyExpoView>(); view.Initialize(50, 40);
                try
                {
                    view.Sync(s, 0, true);
                    Assert.IsNull(view.ScreenPersonId); Assert.AreEqual("cpu0", view.ScreenProductId);
                    int version = view.BuildVersion;
                    var pod = view.LiveRoot.transform.Find("PlayerBooth/ExhibitPod_cpu0");
                    Assert.NotNull(pod.GetComponentInChildren<RawImage>().texture);
                    person.LocationPlaceId = "life_expo";
                    view.Sync(s, 0, true);
                    Assert.AreEqual("qiao", view.ScreenPersonId);
                    Assert.AreEqual(version, view.BuildVersion);
                    person.LocationPlaceId = "home";
                    view.Sync(s, 0, true);
                    Assert.IsNull(view.ScreenPersonId);
                    Assert.AreEqual(version, view.BuildVersion);
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
        }
    }
}
