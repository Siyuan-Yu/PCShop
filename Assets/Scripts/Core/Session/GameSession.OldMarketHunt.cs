using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;

namespace PCShopSell.Core.Session
{
    public sealed class OldMarketHuntSpot
    {
        public readonly string Id, Label, ProductName, Portrait;
        public readonly int Stall, ShelfProduct;
        public readonly double Weight;

        public OldMarketHuntSpot(string id, int stall, string label, string productName, string portrait,
            int shelfProduct, double weight)
        {
            Id = id;
            Stall = stall;
            Label = label;
            ProductName = productName;
            Portrait = portrait;
            ShelfProduct = shelfProduct;
            Weight = weight;
        }
    }

    public static class OldMarketHuntCatalog
    {
        public const int MinutesPerSearch = 40;
        public static readonly string[] Tiers = { "基础档", "中档", "高档" };
        public static readonly int[] Prices = { 500, 1500, 5000 };
        public static readonly int[] PassSizes = { 1, 3, 6 };
        public static readonly string[] Stalls = { "旧电器棚", "钟表杂货摊", "唱片摊" };
        public static readonly string[] GradeNames = { "严重损坏", "普通常见款", "成色很好", "少见型号", "极罕见收藏品" };
        public static readonly string[] GradeDetails =
        {
            "缺少关键零件，可作零件货出售。",
            "有明显使用痕迹，基本功能正常。",
            "保养得当，主要部件齐全。",
            "少见的停产版本，保存完好。",
            "罕见的早期收藏版，原装配件齐全。"
        };
        private static readonly string[] Brands = { "青鹭牌", "湖声牌", "澄川牌" };
        private static readonly string[] Series = { "家用型", "专业型", "旗舰型" };
        private static readonly string[] Editions = { "残件", "常规版", "改良版", "停产版", "早期编号版" };
        private static readonly UsedCondition[] Conditions =
        {
            UsedCondition.Broken, UsedCondition.Flawed, UsedCondition.Good, UsedCondition.Good, UsedCondition.Good
        };

        // Each array uses tenths of a percent. A variant's price is stable for its model and condition.
        private static readonly (int Weight, int Min, int Max)[][] Bands =
        {
            new (int, int, int)[] { (400, 10, 80), (350, 150, 400), (235, 500, 1500), (14, 2000, 3500), (1, 4000, 6500) },
            new (int, int, int)[] { (380, 20, 220), (340, 350, 1000), (258, 1200, 3600), (20, 4500, 8000), (2, 9000, 14000) },
            new (int, int, int)[] { (370, 50, 400), (320, 650, 2300), (280, 3000, 8800), (27, 10000, 18000), (3, 20000, 30000) }
        };
        public static readonly OldMarketHuntSpot[] Spots =
        {
            new OldMarketHuntSpot("lamp", 0, "台灯", "桌面台灯", "used:0", 0, 1.2),
            new OldMarketHuntSpot("radio", 0, "收音机", "收音机", "oldmarket:radio", 1, .8),
            new OldMarketHuntSpot("electronics-box", 0, "纸箱里的旧电子", "磁带随身听", "oldmarket:cassette", 1, .5),
            new OldMarketHuntSpot("watch-box", 1, "表盒里的钟表", "机械怀表", "oldmarket:pocket", 2, .2),
            new OldMarketHuntSpot("clock", 1, "座钟", "木壳座钟", "oldmarket:clock", 2, 2.4),
            new OldMarketHuntSpot("camera-case", 1, "皮匣里的相机", "机械相机", "oldmarket:camera", 5, 1.1),
            new OldMarketHuntSpot("records", 2, "唱片箱里的唱片", "黑胶唱片", "oldmarket:jazz", 3, .5),
            new OldMarketHuntSpot("turntable", 2, "唱机", "黑胶唱机", "oldmarket:record", 7, 2.8),
            new OldMarketHuntSpot("headphones", 2, "旧耳机", "头戴耳机", "oldmarket:cassette", 1, .45)
        };

        public static bool ValidTier(int tier) => tier >= 0 && tier < Prices.Length;
        public static int PriceLimit(int tier) => Bands[tier][Bands[tier].Length - 1].Max;
        public static OldMarketHuntSpot Spot(string id) => string.IsNullOrEmpty(id) ? null : Spots.FirstOrDefault(spot => spot.Id == id);

        internal static UsedItem Draw(OldMarketHuntSpot spot, int tier, int poolRoll, int variant, int id)
        {
            var bands = Bands[tier];
            int grade = 0;
            while (grade < bands.Length - 1 && poolRoll >= bands[grade].Weight)
                poolRoll -= bands[grade++].Weight;
            var band = bands[grade];
            int price = band.Min + (band.Max - band.Min) * variant / (Brands.Length - 1);
            string type = spot.Id == "radio" && tier == 0 ? "便携收音机" :
                spot.Id == "radio" ? "台式收音机" : spot.ProductName;
            return new UsedItem
            {
                Id = id, Product = spot.ShelfProduct, Owned = true, Inspected = true,
                Cost = Prices[tier], Price = price, Condition = Conditions[grade], Claim = Conditions[grade],
                MarketSpotId = spot.Id, MarketTier = tier, MarketGrade = grade, MarketReferencePrice = price,
                MarketModelName = Brands[variant] + type + " · " + Series[tier] + Editions[grade]
            };
        }
    }

    [Serializable]
    public sealed class OldMarketHuntState
    {
        public int Tier, PassSize, Remaining, NextItemId = 1;
        public string ActivityId;
        public int ActivitySearchMinutes;
        public UsedItem Pending;
        public List<UsedItem> Carried = new List<UsedItem>();

        public OldMarketHuntState Copy() => new OldMarketHuntState
        {
            Tier = Tier, PassSize = PassSize, Remaining = Remaining, NextItemId = NextItemId,
            ActivityId = ActivityId, ActivitySearchMinutes = ActivitySearchMinutes,
            Pending = Pending?.Copy(),
            Carried = (Carried ?? new List<UsedItem>()).Where(item => item != null).Select(item => item.Copy()).ToList()
        };
    }

    public sealed partial class GameSession
    {
        private OldMarketHuntState _marketHunt = new OldMarketHuntState();
        public int MarketHuntTier => _marketHunt.Tier;
        public int MarketHuntPassSize => _marketHunt.PassSize;
        public int MarketHuntRemaining => _marketHunt.Remaining;
        public UsedItem PendingMarketFind => _marketHunt.Pending?.Copy();
        public UsedItem[] CarriedMarketFinds => _marketHunt.Carried.Select(item => item.Copy()).ToArray();
        public int CarriedMarketFindCount => _marketHunt.Carried.Count;
        public static bool IsOldMarketVisit(CityActivity activity) =>
            activity?.DefinitionId == "oldmarket_visit" && activity.VenueId == OldMarketVenueId;

        public IEnumerable<string> MarketHuntGiftRecipients
        {
            get
            {
                var activity = RunningCityActivity;
                if (_marketHunt.Pending == null || activity?.VenueId != OldMarketVenueId
                    || CurrentPlace?.Id != OldMarketVenueId || _marketHunt.ActivityId != activity.Id
                    || IsGameOver || _sleeping || IsWorking) yield break;
                foreach (var participant in activity.Participants)
                    if (participant.Phase == CityParticipantPhase.Participating && CitySamePlace(participant.PersonId))
                        yield return participant.PersonId;
            }
        }

        private double CarriedMarketFindKg => _marketHunt.Carried.Sum(UsedWeight);
        private int MarketHuntMinutes(CityActivity activity) => activity != null && _marketHunt.ActivityId == activity.Id
            ? _marketHunt.ActivitySearchMinutes : 0;
        private long MarketHuntStartMinute => Clock.Now.TotalMinutes + MarketHuntMinutes(RunningCityActivity);

        private string MarketHuntVisitBlocker()
        {
            if (CurrentPlace?.Id == OldMarketVenueId && NpcVenueStaffingBlocker(CurrentPlace) is string staffing) return staffing;
            if (!AtOldMarket) return "请到营业中的青湾旧物市场。";
            if (IsGameOver || IsWorking || _sleeping || ManualActivityClockPaused && !OldMarketActivityRunning || BrandExpoExclusive)
                return "请先完成正在进行的活动。";
            if (OutdoorState(RunningCityActivity)?.Pending != null) return "请先完成同行活动中的当前互动。";
            if (_oldMarket.Pending != null) return "请先完成市场里的查看或维修。";
            if (_marketHunt.Pending != null) return "请先领取上一次淘到的旧物。";
            return null;
        }

        public string MarketHuntPassBlocker(int tier, int attempts)
        {
            if (!OldMarketHuntCatalog.ValidTier(tier) || !OldMarketHuntCatalog.PassSizes.Contains(attempts))
                return "请选择一个档位和次数。";
            return MarketHuntVisitBlocker()
                ?? (_marketHunt.Remaining > 0 ? "请先用完已买的次数。"
                    : !CityVenueFits(CurrentPlace, MarketHuntStartMinute, attempts * OldMarketHuntCatalog.MinutesPerSearch)
                        ? "距打烊时间不足，可以选择更少的次数。"
                    : !Ledger.CanAfford(Money.FromYuan(OldMarketHuntCatalog.Prices[tier] * attempts)) ? "现金不足。" : null);
        }

        public bool BuyMarketHuntPass(int tier, int attempts)
        {
            if (MarketHuntPassBlocker(tier, attempts) is string blocker) return Fail(blocker);
            var amount = Money.FromYuan(OldMarketHuntCatalog.Prices[tier] * attempts);
            if (!Ledger.TryDebit(Clock.Today, "淘旧物", amount, OldMarketHuntCatalog.Tiers[tier] + " · " + attempts + "次"))
                return Fail("现金不足。");
            _marketHunt.Tier = tier;
            _marketHunt.PassSize = _marketHunt.Remaining = attempts;
            return true;
        }

        public string MarketHuntSearchBlocker(OldMarketHuntSpot spot)
        {
            if (spot == null) return "请先选一个物件。";
            return MarketHuntVisitBlocker()
                ?? (_marketHunt.Remaining <= 0 ? "请先买淘货次数。"
                    : !CityVenueFits(CurrentPlace, MarketHuntStartMinute, OldMarketHuntCatalog.MinutesPerSearch)
                        ? "快打烊了，这次只能逛逛。"
                    : spot.Weight > FreeKg + 1e-9 ? "背包剩余负重不足。" : null);
        }

        public bool BeginMarketHunt(string spotId)
        {
            var spot = OldMarketHuntCatalog.Spot(spotId);
            if (MarketHuntSearchBlocker(spot) is string blocker) return Fail(blocker);

            // Store the result before advancing time or showing the animation.
            _marketHunt.Pending = OldMarketHuntCatalog.Draw(spot, _marketHunt.Tier, Random.Next(0, 1000),
                Random.Next(0, 3), _marketHunt.NextItemId++);
            _marketHunt.Remaining--;
            if (OldMarketActivityRunning)
            {
                var activity = RunningCityActivity;
                int used = MarketHuntMinutes(activity);
                _marketHunt.ActivityId = activity.Id;
                _marketHunt.ActivitySearchMinutes = used + OldMarketHuntCatalog.MinutesPerSearch;
            }
            else
            {
                _marketHunt.ActivityId = null;
                _marketHunt.ActivitySearchMinutes = 0;
                AdvanceClock(OldMarketHuntCatalog.MinutesPerSearch);
            }
            return true;
        }

        public bool ClaimMarketFind()
        {
            var item = _marketHunt.Pending;
            if (item == null || IsGameOver || !CanCarry(UsedWeight(item))) return false;
            _marketHunt.Carried.Add(item);
            _marketHunt.Pending = null;
            ApplyCargoLimit();
            FinishMarketHuntIfDone();
            return true;
        }

        public bool GiftMarketFindToCompanion(string personId)
        {
            if (!MarketHuntGiftRecipients.Contains(personId)) return Fail("请送给本次活动中仍在场的同行者。");
            var activity = RunningCityActivity;
            string item = UsedItemName(_marketHunt.Pending);
            string name = CityPerson(personId)?.Name ?? "同行者";
            _marketHunt.Pending = null;
            RecordCommunityMarketGift(activity, personId);
            string note = $"把「{item}」送给了{name}。";
            activity.EventSettlementNote = string.IsNullOrEmpty(activity.EventSettlementNote)
                ? note : activity.EventSettlementNote + "\n" + note;
            RememberSocial(personId, $"在青湾旧物市场送给对方「{item}」。");
            Say($"{name}收下了「{item}」。");
            FinishMarketHuntIfDone();
            return true;
        }

        private void FinishMarketHuntIfDone()
        {
            var activity = RunningCityActivity;
            if (_marketHunt.Remaining == 0 && IsOldMarketVisit(activity) && _marketHunt.ActivityId == activity.Id)
                FinishManualCityActivity(activity.Id);
        }

        public bool StoreMarketFindAtUsedShop(int id)
        {
            var shop = CurrentShop;
            if (!SpecialPlayer(shop) || shop.RetailKind != "secondhand") return Fail("请到自己的二手店办理入库。");
            var item = _marketHunt.Carried.FirstOrDefault(find => find.Id == id);
            if (item == null) return false;
            var stock = SpecialBusiness(shop.Place.Id);
            if (UsedStockCount(stock) >= UsedStockLimit(shop)) return Fail("这家二手店库存已满。");
            _marketHunt.Carried.Remove(item);
            item.Id = stock.NextId++;
            stock.Used.Add(item);
            ApplyCargoLimit();
            Say($"{UsedItemName(item)} 已入库，可在收购鉴定台的「待上架」定价。");
            return true;
        }

        private void SaveMarketHunt(SaveData save) => save.oldMarketHunt = _marketHunt.Copy();
        private void RestoreMarketHunt(SaveData save)
        {
            _marketHunt = save.oldMarketHunt?.Copy() ?? new OldMarketHuntState();
            // 空的嵌套对象可能在 JSON 重载时被实例化；真实淘货编号始终从 1 开始。
            if (_marketHunt.Pending != null && _marketHunt.Pending.Id <= 0) _marketHunt.Pending = null;
            if (!OldMarketHuntCatalog.ValidTier(_marketHunt.Tier) || !OldMarketHuntCatalog.PassSizes.Contains(_marketHunt.PassSize))
                _marketHunt.Tier = _marketHunt.PassSize = _marketHunt.Remaining = 0;
            else
                _marketHunt.Remaining = Math.Clamp(_marketHunt.Remaining, 0, _marketHunt.PassSize);
            _marketHunt.NextItemId = Math.Max(1, _marketHunt.NextItemId);
            ApplyCargoLimit();
        }
    }

    public sealed partial class SaveData
    {
        public OldMarketHuntState oldMarketHunt;
    }
}
