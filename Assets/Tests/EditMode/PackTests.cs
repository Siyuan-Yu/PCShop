using System.Collections.Generic;
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
        public void HomeBox_StoresGifts_WithWeightLimit_AndSaves()
        {
            GameSession s = Make();
            GameSessionTests.Teleport(s, "flat");
            s.Backpack.AddGift(Gifts.Drink, 3);
            Assert.IsTrue(s.StoreInBox(Gifts.Drink, 3), "18 公斤放得下");
            Assert.AreEqual(0, s.Backpack.GiftCount(Gifts.Drink));
            Assert.AreEqual(3, s.HomeBoxCount(Gifts.Drink));
            Assert.AreEqual(18.0, s.HomeBoxKg, 1e-9);
            s.Backpack.AddGift(Gifts.Drink, 1);
            Assert.IsFalse(s.StoreInBox(Gifts.Drink), "再放 6 公斤会超过 20 公斤");
            Assert.AreEqual(1, s.Backpack.GiftCount(Gifts.Drink));
            s.Backpack.AddGift(Gifts.Cig, 1);
            Assert.IsTrue(s.StoreInBox(Gifts.Cig), "0.2 公斤放得下");

            GameSession l = GameSession.Load(s.City, JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave())));
            Assert.AreEqual(3, l.HomeBoxCount(Gifts.Drink));
            Assert.AreEqual(1, l.HomeBoxCount(Gifts.Cig));

            Assert.IsTrue(s.TakeFromBox(Gifts.Cig));
            Assert.AreEqual(1, s.Backpack.GiftCount(Gifts.Cig), "拿回背包一份");
            GameSessionTests.Teleport(s, "pc");
            Assert.IsFalse(s.TakeFromBox(Gifts.Drink), "不在家不能用纸箱");
            Assert.IsFalse(s.StoreInBox(Gifts.Drink), "不在家不能用纸箱");
        }

        [Test]
        public void HomeBox_TakeOut_RespectsCarryLimit()
        {
            GameSession s = Make();
            GameSessionTests.Teleport(s, "flat");
            s.Backpack.AddGift(Gifts.Drink, 3);
            Assert.IsTrue(s.StoreInBox(Gifts.Drink, 3));
            s.SetVehicle(VehicleKind.Walk);
            Assert.IsTrue(s.TakeFromBox(Gifts.Drink, 2), "步行最多 12 公斤，两箱 12 公斤刚好");
            Assert.IsFalse(s.TakeFromBox(Gifts.Drink), "再拿就超重了");
            Assert.AreEqual(1, s.HomeBoxCount(Gifts.Drink));
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
            Assert.IsFalse(s.OrderOnline(ParcelKind.Furniture, "aircon"), "刚买的手机里没有网购 app");
            Assert.IsTrue(s.InstallApp(PhoneApps.Shop));
            Assert.IsTrue(s.OrderOnline(ParcelKind.Furniture, "aircon"));
        }

        [Test]
        public void NewPhone_OnlyHasStoreAndContacts_AppsDownloadedFree()
        {
            GameSession s = Make(false);
            GameSessionTests.Teleport(s, "phone");
            Assert.IsTrue(s.BuySmartphone());
            Assert.IsTrue(s.HasApp(PhoneApps.Store));
            Assert.IsTrue(s.HasApp(PhoneApps.Contacts));
            Assert.IsTrue(s.HasApp(PhoneApps.Schedule), "日历是自带的");
            foreach (string id in new[] { PhoneApps.Shop, PhoneApps.Chat, PhoneApps.Wholesale })
                Assert.IsFalse(s.HasApp(id), id);
            Money before = s.Ledger.Balance;
            Assert.IsTrue(s.InstallApp(PhoneApps.Chat));
            Assert.IsFalse(s.InstallApp(PhoneApps.Chat), "不能重复装");
            Assert.AreEqual(before.Cents, s.Ledger.Balance.Cents, "下载免费");
            Assert.IsFalse(s.UninstallApp(PhoneApps.Store));
            Assert.IsTrue(s.UninstallApp(PhoneApps.Chat));
            Assert.IsFalse(s.HasApp(PhoneApps.Chat));
        }

        [Test]
        public void Calendar_RemindsOnlyWithSmartphone()
        {
            foreach (bool phone in new[] { true, false })
            {
                GameSession s = Make(false);
                s.SetSmartphone(phone);
                Assert.AreEqual(phone, s.HasCalendar);
                var notes = new List<string>();
                s.Notice += m => notes.Add(m);
                GameSessionTests.Teleport(s, "flat");
                s.Clock.Restore(s.Clock.Today, 8 * 60);
                long start = (long)s.Clock.Today.TotalDays * 1440 + 13 * 60;
                Assert.IsTrue(s.BookRepair("fan_broken", start));
                s.AdvanceClock(4 * 60);
                Assert.IsFalse(notes.Any(m => m.Contains("日历提醒")), "还早");
                s.AdvanceClock(60);
                Assert.AreEqual(phone, notes.Any(m => m.Contains("日历提醒")), "12:30 提醒（只有智能手机）");
            }
        }
        [Test]
        public void NoPhone_CannotInstallApps()
        {
            GameSession s = Make(false);
            Assert.IsFalse(s.InstallApp(PhoneApps.Shop));
            Assert.IsNotNull(s.AppBlocker(PhoneApps.Shop));
        }

        [Test]
        public void Online1788_NeedsTheApp()
        {
            GameSession s = Make(false);
            GameSessionTests.Teleport(s, "phone");
            s.BuySmartphone();
            GameSessionTests.Teleport(s, "pc");
            var part = PartCatalog.Get("ssd1");
            Assert.IsFalse(s.PlaceOnlineOrder("s_mid", new[] { (part, 1) }), "没装 1788 app");
            Assert.IsTrue(s.InstallApp(PhoneApps.Wholesale));
            Assert.IsTrue(s.PlaceOnlineOrder("s_mid", new[] { (part, 1) }));
        }

        [Test]
        public void Chat_OncePerDay_RaisesRelationUpToCap()
        {
            GameSession s = Make();
            s.Meet(Npcs.Zhang, 20);
            Assert.IsNull(s.ChatBlocker(Npcs.Zhang));
            Assert.IsTrue(s.Chat(Npcs.Zhang));
            Assert.AreEqual(20, s.Relation(Npcs.Zhang));
            Assert.IsFalse(s.Chat(Npcs.Zhang), "今天聊过了");
            s.Clock.Restore(s.Clock.Today.AddDays(1), 10 * 60);
            Assert.IsTrue(s.Chat(Npcs.Zhang));
            Assert.AreEqual(21, s.Relation(Npcs.Zhang));
            s.AddRelation(Npcs.Zhang, 60);
            s.Clock.Restore(s.Clock.Today.AddDays(1), 10 * 60);
            int rel = s.Relation(Npcs.Zhang);
            Assert.IsTrue(s.Chat(Npcs.Zhang));
            Assert.AreEqual(rel, s.Relation(Npcs.Zhang), "到 40 以后聊天不再涨");
        }

        [Test]
        public void Chat_NeedsTheApp_AndAcquaintance()
        {
            GameSession s = Make(false);
            s.Meet(Npcs.Zhang, 20);
            Assert.IsNull(s.ChatBlocker(Npcs.Zhang), "按键机也能发短信");
            Assert.IsFalse(s.HasChatApp);
            Assert.IsNotNull(s.ChatBlocker(Npcs.Lin), "还没认识");
            s.SetSmartphone(true);
            Assert.IsTrue(s.HasChatApp);
        }

        [Test]
        public void Sms_OftenNoReply_ChatAppAlwaysReplies()
        {
            GameSession s = Make(false);
            s.Meet(Npcs.Zhang, 10);
            int replied = 0, days = 80;
            for (int i = 0; i < days; i++)
            {
                s.Clock.Restore(s.Clock.Today.AddDays(1), 10 * 60);
                Assert.IsTrue(s.Chat(Npcs.Zhang));
                if (s.LastChatReplied) replied++;
            }
            Assert.Greater(replied, 5, "偶尔有人回");
            Assert.Less(replied, days - 20, "常常没人回");
            Assert.IsFalse(s.Chat(Npcs.Zhang), "一天一条");

            s.SetSmartphone(true);
            for (int i = 0; i < 10; i++)
            {
                s.Clock.Restore(s.Clock.Today.AddDays(1), 10 * 60);
                Assert.IsTrue(s.Chat(Npcs.Zhang));
                Assert.IsTrue(s.LastChatReplied);
            }
        }

        [Test]
        public void SaveLoad_KeepsInstalledApps_OldSaveHasAll()
        {
            GameSession s = Make(false);
            GameSessionTests.Teleport(s, "phone");
            s.BuySmartphone();
            s.InstallApp(PhoneApps.Wholesale);
            s.Meet(Npcs.Zhang, 20);
            s.Chat(Npcs.Zhang);
            SaveData d = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave()));
            GameSession l = GameSession.Load(s.City, d);
            Assert.IsTrue(l.HasApp(PhoneApps.Wholesale));
            Assert.IsFalse(l.HasApp(PhoneApps.Shop));
            Assert.IsNotNull(l.ChatBlocker(Npcs.Zhang));
            d.apps = null;
            GameSession old = GameSession.Load(s.City, d);
            Assert.IsTrue(old.HasApp(PhoneApps.Shop) && old.HasApp(PhoneApps.Wholesale), "旧存档视为全装了");
        }

        [Test]
        public void NoPhone_Online1788IsLocked_CheatUnlocks()
        {
            GameSession s = Make(false);
            var part = PartCatalog.Get("ssd1");
            GameSessionTests.Teleport(s, "pc");
            Assert.IsFalse(s.PlaceOnlineOrder("s_mid", new[] { (part, 1) }));
            s.SetSmartphone(true);
            Assert.IsTrue(s.HasApp(PhoneApps.Wholesale), "作弊拥有手机时 app 全装好");
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
