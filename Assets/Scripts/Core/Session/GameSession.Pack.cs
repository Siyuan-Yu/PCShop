using System;
using System.Collections.Generic;
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

        /// <summary>作弊 / 调试：直接拥有（或取消）智能手机。</summary>
        public void SetSmartphone(bool has)
        {
            HasSmartphone = has;
        }

        public bool BuySmartphone()
        {
            if (CurrentPlace == null || CurrentPlace.Business != PhoneBusiness) return Fail("要去手机店才能买。");
            if (HasSmartphone) return Fail("你已经有智能手机了。");
            if (!Ledger.TryDebit(Clock.Today, "购物", PhonePrice, "智能手机")) return Fail("钱不够。");
            HasSmartphone = true;
            Say("买了一部智能手机：现在可以网购、上 1788 了。");
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

        private void UnloadFoodAtHome()
        {
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
