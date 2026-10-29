using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;
using UnityEngine;

namespace PCShopSell.Tests
{
    /// <summary>智能手机、背包承重、礼品要先买后送。</summary>
    public class PackTests
    {
        private static GameSession Make(bool phone = true, long cash = 20000)
        {
            CityLayoutData data = GameSessionTests.Layout();
            var places = data.places.ToList();
            places.Add(new LayoutPlaceData { id = "tobacco", name = "烟酒茶行", kind = "shop", node = "dl", size = "small", business = Gifts.TobaccoBusiness });
            places.Add(new LayoutPlaceData { id = "phone", name = "手机店", kind = "shop", node = "st2", size = "small", business = GameSession.PhoneBusiness });
            data.places = places.ToArray();
            return new GameSession(CityLayout.FromData(data), 7, Money.FromYuan(cash), GameTimestamp.From(new GameDate(0), 9 * 60), true, phone);
        }

        [Test]
        public void NoPhone_CannotShopOnline_UntilBought()
        {
            GameSession s = Make(false);
            GameSessionTests.Teleport(s, "flat");
            Assert.IsFalse(s.HasSmartphone);
            Assert.IsFalse(s.OrderOnline(ParcelKind.Furniture, "aircon"), "没手机不能网购");
            Assert.IsFalse(s.BuySmartphone(), "不在手机店");
            GameSessionTests.Teleport(s, "phone");
            Money before = s.Ledger.Balance;
            Assert.IsTrue(s.BuySmartphone());
            Assert.AreEqual((before - GameSession.PhonePrice).Cents, s.Ledger.Balance.Cents);
            Assert.IsTrue(s.HasSmartphone);
            Assert.IsFalse(s.BuySmartphone(), "已经有了");
            GameSessionTests.Teleport(s, "flat");
            Assert.IsTrue(s.OrderOnline(ParcelKind.Furniture, "aircon"));
        }

        [Test]
        public void NoPhone_Online1788IsLocked_CheatUnlocks()
        {
            GameSession s = Make(false);
            var part = PartCatalog.Get("ssd1");
            GameSessionTests.Teleport(s, "pc");
            Assert.IsFalse(s.PlaceOnlineOrder("s_mid", new[] { (part, 1) }));
            s.SetSmartphone(true);
            Assert.IsTrue(s.PlaceOnlineOrder("s_mid", new[] { (part, 1) }));
        }

        [Test]
        public void Gifts_MustBeBoughtFirst_ThenGiven()
        {
            GameSession s = Make();
            s.Meet(Npcs.Zhang, 20);
            Assert.IsNotNull(s.GiftBlocker(Npcs.Zhang));
            Assert.IsFalse(s.BuyGift(Gifts.Cig), "不在烟酒茶行");
            GameSessionTests.Teleport(s, "tobacco");
            Money before = s.Ledger.Balance;
            Assert.IsTrue(s.BuyGift(Gifts.Cig));
            Assert.AreEqual((before - Gifts.Get(Gifts.Cig).Price).Cents, s.Ledger.Balance.Cents);
            Assert.AreEqual(1, s.Backpack.GiftCount(Gifts.Cig));
            Assert.IsNull(s.GiftBlocker(Npcs.Zhang));
            Assert.IsTrue(s.GiveGift(Npcs.Zhang));
            Assert.AreEqual(0, s.Backpack.GiftCount(Gifts.Cig));
        }

        [Test]
        public void Backpack_HasWeightLimit_ByVehicle()
        {
            GameSession s = Make();
            GameSessionTests.Teleport(s, "tobacco");
            double limit = s.CarryLimitKg;
            Assert.Greater(limit, 0);
            Assert.IsTrue(s.BuyGift(Gifts.Drink, 2), "两箱饮料刚好");
            Assert.AreEqual(Gifts.Get(Gifts.Drink).WeightKg * 2, s.CarriedKg, 1e-9);
            Assert.IsFalse(s.BuyGift(Gifts.Drink, 1) && s.CarriedKg > limit + 1e-9);
            Assert.LessOrEqual(s.CarriedKg, limit + 1e-9);
        }

        [Test]
        public void Backpack_ShrinksCargoSpace()
        {
            GameSession s = Make();
            double free = s.Cargo.Capacity;
            GameSessionTests.Teleport(s, "tobacco");
            Assert.IsTrue(s.BuyGift(Gifts.Drink, 1));
            Assert.Less(s.Cargo.Capacity, free);
        }

        [Test]
        public void HeavyRice_CannotBeCarriedOnFoot()
        {
            GameSession s = Make();
            GameSessionTests.Teleport(s, "tobacco");
            Assert.IsFalse(s.BuyRice(60), "60 斤米太重");
        }

        [Test]
        public void OnlineGift_ArrivesInBackpack()
        {
            GameSession s = Make();
            GameSessionTests.Teleport(s, "flat");
            Assert.IsTrue(s.OrderOnline(ParcelKind.Gift, Gifts.CigHi));
            Assert.AreEqual(0, s.Backpack.GiftCount(Gifts.CigHi));
            int day = s.Clock.Today.TotalDays + 3;
            while (s.Clock.Today.TotalDays < day) s.AdvanceClock(60);
            Assert.AreEqual(1, s.Backpack.GiftCount(Gifts.CigHi));
        }

        [Test]
        public void SaveLoad_KeepsPhoneAndBackpack()
        {
            GameSession s = Make(false);
            s.Backpack.AddGift(Gifts.Wine, 2);
            s.Backpack.Food.RiceJin = 3;
            SaveData d = s.CreateSave();
            GameSession l = GameSession.Load(s.City, JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(d)));
            Assert.IsFalse(l.HasSmartphone);
            Assert.AreEqual(2, l.Backpack.GiftCount(Gifts.Wine));
            Assert.AreEqual(3.0, l.Backpack.Food.RiceJin, 1e-9);

            s.SetSmartphone(true);
            GameSession l2 = GameSession.Load(s.City, JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave())));
            Assert.IsTrue(l2.HasSmartphone);
        }
    }
}
