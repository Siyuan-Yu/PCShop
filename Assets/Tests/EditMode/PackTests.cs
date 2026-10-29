using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Inventory;
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
            var s = new GameSession(CityLayout.FromData(data), 7, Money.FromYuan(cash), GameTimestamp.From(new GameDate(0), 9 * 60), true, phone);
            EmptyBag(s);
            return s;
        }

        /// <summary>倒掉开局带的饭盒，好按整数算负重。</summary>
        public static void EmptyBag(GameSession s)
        {
            s.Backpack.Food.Packed.Clear();
            s.ApplyCargoLimit();
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
            Assert.IsFalse(s.TakeFromBox(Gifts.Drink), "店里拿的是储物柜，家里纸箱的东西拿不到");
            Assert.IsTrue(s.StoreInBox(Gifts.Cig), "店里能放进储物柜");
            Assert.AreEqual(1, s.ShopLocker.GiftCount(Gifts.Cig));
            Assert.AreEqual(3, s.HomeBoxCount(Gifts.Drink), "家里纸箱不受影响");
        }

        [Test]
        public void ShopLocker_And_CourierDest_SurviveSave()
        {
            GameSession s = Make();
            GameSessionTests.Teleport(s, "pc");
            s.Backpack.AddGift(Gifts.Cig, 2);
            Assert.IsTrue(s.StoreInBox(Gifts.Cig, 2), "店里放进储物柜");
            Assert.AreEqual(2, s.ShopLocker.GiftCount(Gifts.Cig));
            Assert.AreEqual(0, s.HomeBoxCount(Gifts.Cig));
            Assert.IsFalse(s.CourierToShop(false), "没选时按这家店的默认");
            s.CourierDest = true;
            Assert.IsTrue(s.CourierToShop(false), "选了送店里就送店里");

            GameSession l = GameSession.Load(s.City, JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave())));
            Assert.AreEqual(2, l.ShopLocker.GiftCount(Gifts.Cig), "储物柜存档后还在");
        }

        [Test]
        public void HomeBox_HoldsAnything_OnlyWeightLimit()
        {
            GameSession s = Make();
            GameSessionTests.Teleport(s, "flat");
            PartDef ram = PartCatalog.Get("ram0");
            s.Cargo.ForceAdd(ram, true, 2);
            s.Backpack.Food.RiceJin = 4;
            s.Backpack.Food.AddIngredient("egg", 3, s.Clock.Today.TotalDays);
            s.Backpack.Food.Packed.Add(new FoodBatch { Id = "rice", Name = "白米饭", Hunger = 22, Count = 2, ExpireDay = s.Clock.Today.TotalDays + 1 });
            s.Backpack.AddFurniture("plant", 1);
            s.ApplyCargoLimit();

            Assert.IsTrue(s.StoreInBox("part:ram0:b", 2), "配件也能放");
            Assert.IsTrue(s.StoreInBox("rice", 4), "米按斤放");
            Assert.IsTrue(s.StoreInBox("ing:egg", 3));
            Assert.IsTrue(s.StoreInBox("lunch", 2), "饭盒也能放");
            Assert.IsTrue(s.StoreInBox("furn:plant"), "小家具也能放");
            Assert.IsTrue(s.Backpack.IsEmpty && s.Cargo.IsEmpty, "背包清空了");
            double kg = 2 * Stock.SpaceOf(ram, true, 1) * Backpack.KgPerSpace + 4 * Backpack.RiceKgPerJin + 3 * Backpack.IngredientKg("egg") + 2 * Backpack.LunchboxKg + FurnitureCatalog.Get("plant").WeightKg;
            Assert.AreEqual(kg, s.HomeBoxKg, 1e-9);

            s.Backpack.AddFurniture("desk", 1);
            Assert.IsFalse(s.StoreInBox("furn:desk"), "25 公斤的书桌超过纸箱上限");
            s.Backpack.TakeFurniture("desk", 1);
            s.ApplyCargoLimit();

            GameSession l = GameSession.Load(s.City, JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave())));
            Assert.AreEqual(kg, l.HomeBoxKg, 1e-9, "纸箱里的东西存档后都在");
            Assert.AreEqual(2, l.HomeBoxParts.Count(ram, true));
            Assert.AreEqual(2, l.HomeBox.Lunchboxes);

            Assert.IsTrue(s.TakeFromBox("lunch"));
            Assert.AreEqual(1, s.Backpack.Lunchboxes, "饭盒拿回背包");
            Assert.IsTrue(s.TakeFromBox("part:ram0:b", 2));
            Assert.AreEqual(2, s.Cargo.Count(ram, true));
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
