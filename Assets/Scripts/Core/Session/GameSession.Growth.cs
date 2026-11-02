using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Time;
using PCShopSell.Core.Shop;

namespace PCShopSell.Core.Session
{
    /// <summary>衣服。穿在身上加魅力（影响砍价等）。</summary>
    public sealed partial class GameSession
    {
        private HashSet<string> _upgrades => _focus.Upgrades;
        private readonly HashSet<string> _ownedOutfits = new HashSet<string> { Outfits.Default };
        private readonly List<(int day, long cents, string memo)> _scheduledCredits = new List<(int, long, string)>();

        public IReadOnlyCollection<string> Upgrades => _upgrades;
        public IReadOnlyCollection<string> OwnedOutfits => _ownedOutfits;
        public string EquippedOutfit { get; private set; } = Outfits.Default;

        /// <summary>一次性标记（事件里用来记"做过什么"）。</summary>
        public HashSet<string> Flags { get; } = new HashSet<string>();

        // ---------- 店铺升级 ----------

        public bool HasUpgrade(string id) => _upgrades.Contains(id);

        /// <summary>不能买时给出原因。</summary>
        public string UpgradeBlocker(ShopUpgradeDef u)
        {
            if (_upgrades.Contains(u.Id)) return "已拥有";
            if (!u.Offered) return "这里不卖";
            if (!HasShop) return "先去房产中介租一间铺子";
            if (u.NeedsSmartphone && !HasSmartphone) return "先去手机店买智能手机";
            if (u.Requires != null && !_upgrades.Contains(u.Requires)) return "需要先买：" + ShopUpgrades.Get(u.Requires).Name;
            if (PlayerShop.Reputation < u.MinReputation) return $"知名度要到 {u.MinReputation:0}";
            if (Ledger.Balance < u.Price) return "钱不够";
            return null;
        }

        /// <summary>直接得到一项升级（任务奖励、老存档迁移、测试用），不扣钱。</summary>
        public bool GrantUpgrade(string id)
        {
            if (!ShopUpgrades.Exists(id)) return false;
            if (_upgrades.Add(id)) ApplyUpgrades();
            return true;
        }

        public bool BuyUpgrade(string id)
        {
            ShopUpgradeDef u = ShopUpgrades.Get(id);
            if (u == null) return false;
            if (CurrentPlace == null || CurrentPlace.Business != u.SoldAt) return Fail($"「{u.Name}」要去{u.SoldAtName}买。");
            string blocker = UpgradeBlocker(u);
            if (blocker != null) return Fail(blocker);
            if (!Ledger.TryDebit(Clock.Today, "装修", u.Price, u.Name)) return Fail("钱不够。");
            _upgrades.Add(id);
            ApplyUpgrades();
            Say($"买了「{u.Name}」，当天给你装到店里了。");
            return true;
        }

        /// <summary>把所有升级的效果套到店铺上（幂等）。</summary>
        internal void ApplyUpgrades()
        {
            PaymentMethods acc = PaymentMethods.Cash;
            if (_upgrades.Contains(ShopUpgrades.Qr)) acc |= PaymentMethods.QrCode;
            if (_upgrades.Contains(ShopUpgrades.SelfCheckout)) acc |= PaymentMethods.SelfCheckout;
            PlayerShop.Accepted = acc;
            PlayerShop.Stock.Capacity = WarehouseCapacity;
            PlayerShop.TrafficMultiplier = (_upgrades.Contains(ShopUpgrades.Sign) ? ShopUpgrades.SignTrafficBonus : 1.0) * NpcTrafficMultiplier() * ShopDecorTrafficMultiplier * CreatorTrafficMultiplier;
            PlayerShop.Settings.PatienceMinutes = _basePatience + ShopDecorPatience;
            UpdateShopFit();
        }

        // ---------- 衣着 ----------

        public OutfitDef Outfit => Outfits.Get(EquippedOutfit) ?? Outfits.Get(Outfits.Default);

        /// <summary>基础魅力 + 衣服加成。</summary>
        public int EffectiveCharm => Player.Charm + Outfit.CharmBonus + (_owned.Contains(VehicleKind.Car) ? 2 : 0);

        /// <summary>换上一件已经有的衣服（在衣柜里）。</summary>
        /// <summary>调试 / 冒烟：直接拥有一件衣服。</summary>
        public void GrantOutfit(string id)
        {
            if (Outfits.Get(id) != null) _ownedOutfits.Add(id);
        }

        public bool Equip(string id)
        {
            if (!_ownedOutfits.Contains(id)) return false;
            if (!CanChangeCityClothing) return Fail("请到家中衣柜或真实试衣镜旁更衣。");
            EquippedOutfit = id;
            return true;
        }

        // ---------- 延迟到账 ----------

        /// <summary>几天后入账一笔钱（借出去又还回来之类）。</summary>
        public void ScheduleCredit(int inDays, Money amount, string memo)
        {
            _scheduledCredits.Add((Clock.Today.TotalDays + inDays, amount.Cents, memo));
        }

        private void ProcessScheduledCredits(GameDate today)
        {
            for (int i = _scheduledCredits.Count - 1; i >= 0; i--)
            {
                var c = _scheduledCredits[i];
                if (c.day > today.TotalDays) continue;
                _scheduledCredits.RemoveAt(i);
                Ledger.Credit(today, "事件", Money.FromCents(c.cents), c.memo);
                Popup?.Invoke("钱回来了", c.memo + "：" + Money.FromCents(c.cents));
            }
        }

        private void RestoreGrowth(SaveData d)
        {
            RestoreCampaign(d);
            RestoreNpcs(d);
            RestoreSocial(d);
            _upgrades.Clear();
            foreach (string id in d.upgrades ?? Array.Empty<string>())
                if (ShopUpgrades.Exists(id)) _upgrades.Add(id);
            ApplyUpgrades();

            _ownedOutfits.Clear();
            _ownedOutfits.Add(Outfits.Default);
            foreach (string id in d.outfits ?? Array.Empty<string>())
                if (Outfits.Get(id) != null) _ownedOutfits.Add(id);
            EquippedOutfit = d.outfitEquipped != null && _ownedOutfits.Contains(d.outfitEquipped) ? d.outfitEquipped : Outfits.Default;
            RestoreHomeData(d);
            MigrateShopFixtures(d);

            Flags.Clear();
            foreach (string f in d.flags ?? Array.Empty<string>()) Flags.Add(f);

            _scheduledCredits.Clear();
            int n = d.scheduledDays?.Length ?? 0;
            for (int i = 0; i < n; i++)
                _scheduledCredits.Add((d.scheduledDays[i], d.scheduledCents[i], d.scheduledMemos[i]));

            RestoreEvents(d);
            RestoreRestock(d);
        }

        private void SaveGrowth(SaveData d)
        {
            d.upgrades = _upgrades.ToArray();
            d.outfits = _ownedOutfits.ToArray();
            d.outfitEquipped = EquippedOutfit;
            d.homeComfort = 0;
            SaveHomeData(d);
            d.flags = Flags.ToArray();
            d.scheduledDays = _scheduledCredits.Select(c => c.day).ToArray();
            d.scheduledCents = _scheduledCredits.Select(c => c.cents).ToArray();
            d.scheduledMemos = _scheduledCredits.Select(c => c.memo).ToArray();
            SaveEvents(d);
            SaveRestock(d);
            SaveCampaign(d);
            SaveNpcs(d);
            SaveSocial(d);
        }
    }
}
