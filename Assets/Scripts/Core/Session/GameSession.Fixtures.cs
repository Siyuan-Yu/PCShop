using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Shop;

namespace PCShopSell.Core.Session
{
    /// <summary>开店清单里的一项：要几个、摆了几个、买了还没摆几个。</summary>
    public sealed class FixtureNeed
    {
        public string Name;
        public string FurnitureId;
        public FixtureKind Kind;

        /// <summary>建议摆的数量（货架 6 个、收银台 1、展示桌、操作台 1）。</summary>
        public int Want;

        public int Placed;

        /// <summary>买了还没摆出来的（家具背包、手上、在路上的包裹）。</summary>
        public int Owned;

        /// <summary>是不是开业的最低要求。</summary>
        public bool Required;
    }

    /// <summary>
    /// 租来的铺子是空的：货架（每个摆一种货，建议 6 个）、收银台、整机展示桌、装机操作台，都得去家具城买回来、
    /// 歇业装修时在店里摆好。至少摆好收银台和一个货架，第二天才开得了业。
    /// </summary>
    public sealed partial class GameSession
    {
        public int PlacedFixtures(FixtureKind kind) => FurnitureCatalog.CountFixtures(_shopLayout, kind);

        /// <summary>摆好了最低要求（收银台 + 至少一个货架）。</summary>
        public bool ShopFitted => PlacedFixtures(FixtureKind.Counter) >= 1 && PlacedFixtures(FixtureKind.Shelf) >= FurnitureCatalog.MinShelves;

        /// <summary>开店还差什么（人话）；摆齐了返回 null。</summary>
        public string ShopFitGap()
        {
            var gaps = new List<string>();
            if (PlacedFixtures(FixtureKind.Counter) < 1) gaps.Add("收银台");
            if (PlacedFixtures(FixtureKind.Shelf) < FurnitureCatalog.MinShelves) gaps.Add("至少一个货架");
            return gaps.Count == 0 ? null : string.Join("、", gaps);
        }

        /// <summary>放着没摆的某件家具：以前收着的、家里和各家店的纸箱 / 临时存放（不算背包里和在路上的）。</summary>
        public int FurnitureStored(string id)
        {
            int n = FurnitureInStock(id) + HomeBox.FurnitureCount(id) + HomeTemp.FurnitureCount(id);
            foreach (OwnedShop s in _shops) n += s.Locker.FurnitureCount(id) + s.Temp.FurnitureCount(id);
            return n;
        }

        /// <summary>有、但还没摆出来的某件家具：放着的、背包里、在路上的。</summary>
        public int FurnitureOwnedUnplaced(string id)
        {
            int n = FurnitureStored(id) + Backpack.FurnitureCount(id);
            foreach (Parcel p in _parcels)
                if (p.Kind == ParcelKind.Furniture && FurnitureCatalog.Canonical(p.Id) == id) n += p.Count;
            foreach (CourierRun r in Couriers)
                foreach (CourierItem item in r.Items)
                    if (item.Kind == CourierItemKind.Furniture && FurnitureCatalog.Canonical(item.Id) == id) n += item.Count;
            foreach (MallCartLine l in _mallStash)
                if (FurnitureCatalog.Canonical(l.Id) == id) n += l.Count;
            return n;
        }

        /// <summary>开店清单：每种设施要几个、摆了几个、买了几个还没摆。</summary>
        public IReadOnlyList<FixtureNeed> ShopFitList()
        {
            var l = new List<FixtureNeed>
            {
                Need(FurnitureCatalog.ShopCounter, 1, true),
            };
            l.Add(Need(FurnitureCatalog.Shelf, FurnitureCatalog.ShelfCategories.Length, true));
            l.Add(Need(FurnitureCatalog.ShopDisplay, Math.Max(1, ShopSettings.DisplaySlots(PlayerShop.Size) / 2), false));
            l.Add(Need(FurnitureCatalog.ShopBench, 1, false));
            return l;

            FixtureNeed Need(string id, int want, bool required)
            {
                FurnitureDef d = FurnitureCatalog.Get(id);
                return new FixtureNeed
                {
                    Name = d.Name, FurnitureId = id, Kind = d.Fixture, Want = want, Required = required,
                    Placed = d.Fixture == FixtureKind.Shelf ? PlacedFixtures(FixtureKind.Shelf) : _shopLayout.Count(p => p.Id == id),
                    Owned = d.Fixture == FixtureKind.Shelf ? FurnitureCatalog.All.Where(x => x.Fixture == FixtureKind.Shelf).Sum(x => FurnitureOwnedUnplaced(x.Id)) : FurnitureOwnedUnplaced(id),
                };
            }
        }

        /// <summary>摆放有变化后：开店资格、展示桌数量（每张桌两个机位）跟着摆出来的东西走。</summary>
        internal void UpdateShopFit()
        {
            PlayerShop.NotFitted = HasShop && !ShopFitted;
            PlayerShop.FixedDisplayDesks = PlacedFixtures(FixtureKind.Display);
            ApplyShelfCapacity();
            // 展示桌被收走了：多出来的整机拆回散件放进库存
            while (PlayerShop.DisplayPcs.Count > PlayerShop.DisplaySlots)
                if (!PlayerShop.DisassembleDisplay(PlayerShop.DisplayPcs[PlayerShop.DisplayPcs.Count - 1])) break;
        }

        /// <summary>测试 / 调试：直接给店摆上整套标准设施。</summary>
        public void DebugFitShop() => FitStarterShop();

        /// <summary>测试 / 调试：直接给家具库存加几件家具。</summary>
        public void DebugGiveFurniture(string id, int count) => AddFurniture(id, count);

        /// <summary>给这间铺子摆上整套标准设施（作弊开店、旧存档迁移、测试用）。已经摆了设施的不动。</summary>
        internal void FitStarterShop()
        {
            if (_shopLayout.Any(p => FurnitureCatalog.Get(p.Id)?.IsFixture == true)) return;
            _shopLayout.InsertRange(0, FurnitureCatalog.DefaultShopFixtures(PlayerShop.Size));
            GiveStarterBoxes(_focus);
            UpdateShopFit();
        }

        /// <summary>
        /// 新租的铺子送 <see cref="StarterShopBoxes"/> 个纸箱，从左前角沿左墙往里摞（摆不下的放进“我的”）。每家店只送一次。
        /// </summary>
        internal void GiveStarterBoxes(OwnedShop shop)
        {
            if (shop == null || !shop.Rented || shop.BoxesGiven) return;
            shop.BoxesGiven = true;
            RoomSpec room = RoomSpec.ForShop(shop.Place.Size);
            FurnitureDef def = FurnitureCatalog.Get(FurnitureCatalog.ShopBox);
            int left = StarterShopBoxes;
            for (double x = 0.3; x < room.Width - 0.3 && left > 0; x += 0.25)
                for (double z = 0.25; z < room.Depth - 0.25 && left > 0; z += 0.25)
                {
                    PlacedFurniture cand = RoomPlanner.Snap(room, def, x, z, 0);
                    if (RoomPlanner.Check(room, shop.Layout, def, cand, -1, null) != null) continue;
                    shop.Layout.Add(cand);
                    left--;
                }
            if (left > 0) AddFurniture(def.Id, left);
        }

        /// <summary>旧存档（设施还是店里自带的时候）：给已经有店的存档补上整套设施，保持原来的样子。</summary>
        private void MigrateShopFixtures(SaveData d)
        {
            int v = d.home == null ? 0 : d.home.fixturesV;
            if (v < 1 && HasShop) FitStarterShop();
            UpdateShopFit();
        }

        // ---------- 中介费打折 ----------

        /// <summary>和中介小陈的熟络度。</summary>
        public int AgentRapport => StaffRapport(Rosters.AgentChen.Id);

        /// <summary>现在的中介费折扣（0 ~ 0.5）。</summary>
        public double AgentDiscount => RealEstate.AgentDiscount(AgentRapport);

        /// <summary>这间月租的铺子，现在中介费要付多少（已打折）。</summary>
        public Money AgentFeeFor(Money rent) => RealEstate.AgentFeeAfter(rent, AgentRapport);
    }
}
