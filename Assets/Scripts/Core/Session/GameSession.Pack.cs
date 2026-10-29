using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;

namespace PCShopSell.Core.Session
{
    /// <summary>智能手机（网购、1788 要用）、背包的承重、烟酒茶行和手机店。</summary>
    public sealed partial class GameSession
    {
        public const string PhoneBusiness = "手机";
        public static readonly Money PhonePrice = Money.FromYuan(800);
        public const string NoPhoneMessage = "你还没有智能手机，上不了网。去街上的手机店买一部。";

        /// <summary>有没有智能手机：没有就用不了网购和 1788。新游戏开局没有。</summary>
        public bool HasSmartphone { get; private set; } = true;

        /// <summary>背包：刚买的米菜、礼品。</summary>
        public Backpack Backpack { get; } = new Backpack();

        private readonly HashSet<string> _apps = new HashSet<string>(PhoneApps.All.Select(a => a.Id));

        /// <summary>手机里装了的 app。</summary>
        public IReadOnlyCollection<string> InstalledApps => _apps;

        /// <summary>作弊 / 调试：直接拥有（或取消）智能手机；拥有时所有 app 都已装好。</summary>
        public void SetSmartphone(bool has)
        {
            HasSmartphone = has;
            _apps.Clear();
            if (has)
                foreach (PhoneAppDef a in PhoneApps.All) _apps.Add(a.Id);
        }

        public bool HasApp(string id) => HasSmartphone && (_apps.Contains(id) || PhoneApps.Preinstalled.Contains(id));

        /// <summary>智能手机自带真正的日历（有提醒）；没有智能手机就只有手写的笔记本。</summary>
        public bool HasCalendar => HasSmartphone;

        /// <summary>要用某个 app 但用不了的原因；能用返回 null。</summary>
        public string AppBlocker(string id)
        {
            if (!HasSmartphone) return NoPhoneMessage;
            if (!_apps.Contains(id) && !PhoneApps.Preinstalled.Contains(id)) return $"手机上没装「{PhoneApps.Get(id)?.Name ?? id}」，去应用商店下载。";
            return null;
        }

        /// <summary>在应用商店下载 app（免费）。</summary>
        public bool InstallApp(string id)
        {
            if (!HasSmartphone) return Fail(NoPhoneMessage);
            if (PhoneApps.Get(id) == null) return false;
            if (!_apps.Contains(PhoneApps.Store)) return Fail("应用商店不见了。");
            if (_apps.Contains(id)) return Fail("已经装过了。");
            _apps.Add(id);
            Say($"下载安装了「{PhoneApps.Get(id).Name}」。");
            return true;
        }

        /// <summary>卸载（应用商店、通讯录、日历不能卸）。</summary>
        public bool UninstallApp(string id)
        {
            if (PhoneApps.Preinstalled.Contains(id)) return Fail("系统自带的，卸不了。");
            return _apps.Remove(id);
        }

        public bool BuySmartphone()
        {
            if (CurrentPlace == null || CurrentPlace.Business != PhoneBusiness) return Fail("要去手机店才能买。");
            if (HasSmartphone) return Fail("你已经有智能手机了。");
            if (!Ledger.TryDebit(Clock.Today, "购物", PhonePrice, "智能手机")) return Fail("钱不够。");
            HasSmartphone = true;
            _apps.Clear();
            foreach (string id in PhoneApps.Preinstalled) _apps.Add(id);
            Say("买了一部智能手机。里面只有应用商店、通讯录和日历，要网购、上 1788 先去应用商店下载 app。");
            return true;
        }

        // ---------- 家里的纸箱 ----------

        /// <summary>纸箱最多放多少公斤。</summary>
        public const double HomeBoxLimitKg = 20;

        private readonly Dictionary<string, int> _boxGifts = new Dictionary<string, int>();

        public IEnumerable<KeyValuePair<string, int>> HomeBoxLines => _boxGifts.Where(kv => kv.Value > 0);

        public int HomeBoxCount(string giftId) => _boxGifts.TryGetValue(giftId, out int n) ? n : 0;

        public double HomeBoxKg => _boxGifts.Sum(kv => kv.Value * (Gifts.Get(kv.Key)?.WeightKg ?? 0));

        private bool AtHome => CurrentPlace != null && CurrentPlace == Home;

        /// <summary>把背包里的一份礼品放进家里的纸箱。</summary>
        public bool StoreInBox(string giftId, int count = 1)
        {
            GiftDef g = Gifts.Get(giftId);
            if (g == null || count <= 0) return false;
            if (!AtHome) return Fail("要在家里才能用纸箱。");
            if (Backpack.GiftCount(giftId) < count) return Fail("背包里没有这么多。");
            if (HomeBoxKg + g.WeightKg * count > HomeBoxLimitKg + 1e-9)
                return Fail($"纸箱放不下了：最多 {HomeBoxLimitKg:0.#} 公斤，已经 {HomeBoxKg:0.#} 公斤。");
            for (int i = 0; i < count; i++) Backpack.TakeGift(giftId);
            _boxGifts[giftId] = HomeBoxCount(giftId) + count;
            ApplyCargoLimit();
            Say($"把「{g.Name}」{(count > 1 ? "×" + count + " " : "")}放进了纸箱。");
            return true;
        }

        /// <summary>从纸箱里拿一份礼品放进背包（不能超过出行方式的承重）。</summary>
        public bool TakeFromBox(string giftId, int count = 1)
        {
            GiftDef g = Gifts.Get(giftId);
            if (g == null || count <= 0) return false;
            if (!AtHome) return Fail("要在家里才能用纸箱。");
            if (HomeBoxCount(giftId) < count) return Fail("纸箱里没有这么多。");
            if (!CanCarry(g.WeightKg * count)) return false;
            int left = HomeBoxCount(giftId) - count;
            if (left <= 0) _boxGifts.Remove(giftId);
            else _boxGifts[giftId] = left;
            Backpack.AddGift(giftId, count);
            ApplyCargoLimit();
            Say($"从纸箱里拿了「{g.Name}」{(count > 1 ? "×" + count + " " : "")}放进背包。");
            return true;
        }

        // ---------- 承重 ----------

        /// <summary>现在的出行方式最多能背 / 载多少公斤。</summary>
        public double CarryLimitKg => CurrentVehicle.CargoCapacity * Backpack.KgPerSpace;

        /// <summary>现在身上的货（进货的配件 + 背包里的东西）合计多少公斤。</summary>
        public double CarriedKg => Cargo.UsedSpace * Backpack.KgPerSpace + Backpack.WeightKg;

        public double FreeKg => Math.Max(0, CarryLimitKg - CarriedKg);

        /// <summary>背包里的东西占了载货的一部分，进货能装的格子相应少一点。</summary>
        private void ApplyCargoLimit()
        {
            Cargo.Capacity = Math.Max(0, CurrentVehicle.CargoCapacity - Backpack.Space);
        }

        private bool CanCarry(double kg)
        {
            if (kg <= FreeKg + 1e-9) return true;
            return Fail($"拿不动了：再背 {kg:0.#} 公斤会超重（{CurrentVehicle.Name}最多 {CarryLimitKg:0.#} 公斤，已经 {CarriedKg:0.#} 公斤）。");
        }

        // ---------- 买礼品 ----------

        private bool AtBusiness(string business) => CurrentPlace != null && CurrentPlace.Business == business;

        /// <summary>在烟酒茶行 / 农贸市场买礼品，放进背包。</summary>
        public bool BuyGift(string id, int count = 1)
        {
            GiftDef g = Gifts.Get(id);
            if (g == null || count <= 0) return false;
            if (!AtBusiness(g.SoldAt)) return Fail(g.SoldAt == Gifts.FarmBusiness ? "水果要去农贸市场买。" : "要去烟酒茶行买。");
            if (g.SoldAt == FarmBusiness && !IsFarmMarketOpen) return Fail("农贸市场收摊了（6:00–18:00）。");
            if (!CanCarry(g.WeightKg * count)) return false;
            if (!Ledger.TryDebit(Clock.Today, "人情", g.Price * count, g.Name + (count > 1 ? "×" + count : ""))) return Fail("钱不够。");
            Backpack.AddGift(id, count);
            ApplyCargoLimit();
            Say($"买了「{g.Name}」放进背包。");
            return true;
        }

        // ---------- 回家把菜米放进厨房 ----------

        /// <summary>背着的轻便家具放进家具库（回家、回店里时）。</summary>
        private void UnloadFurniture()
        {
            var lines = Backpack.FurnitureLines.ToList();
            if (lines.Count == 0) return;
            foreach (var kv in lines) AddFurniture(kv.Key, kv.Value);
            Backpack.ClearFurniture();
            ApplyCargoLimit();
            Say("背着的家具放进了家具背包，点“我的”→摆放家具。");
        }

        private void UnloadFoodAtHome()
        {
            UnloadFurniture();
            Pantry food = Backpack.Food;
            if (food.RiceJin <= 0 && food.Ingredient.Count == 0) return;
            double rice = food.RiceJin;
            Pantry.RiceJin += rice;
            food.RiceJin = 0;
            foreach (FoodBatch b in food.Ingredient) Pantry.Ingredient.Add(b);
            food.Ingredient.Clear();
            ApplyCargoLimit();
            Say("买来的米和菜放进了厨房。");
        }

        /// <summary>背包里的菜隔天也会坏。</summary>
        private void SpoilBackpackFood(int today)
        {
            List<string> lost = Backpack.Food.Spoil(today);
            if (lost.Count > 0)
            {
                Say("背包里放坏扔掉了：" + string.Join("、", lost) + "。");
                ApplyCargoLimit();
            }
        }
    }
}
