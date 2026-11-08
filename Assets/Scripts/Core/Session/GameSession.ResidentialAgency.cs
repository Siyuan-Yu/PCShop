using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Home;
using PCShopSell.Core.World;
using System.Collections.Generic;
using System.Linq;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        public string ResidenceName(int tier) => tier == 3 ? "花园独栋别墅" : tier == 2 ? "公园豪华公寓" : tier == 1 ? "宜居改善住宅" : "城郊老破小出租屋";
        public Place ResidencePlace(int tier)
        {
            string id = tier == 0 ? "home" : "home_tier" + tier;
            return tier >= 0 && tier <= 3 && City.HasPlace(id) ? City.GetPlace(id) : null;
        }
        public Money ResidencePrice(int tier) => Money.FromYuan(tier == 3 ? 1800000 : tier == 2 ? 450000 : tier == 1 ? 180000 : 0).Scale(HomeSaleFactor);
        public Money ResidenceMoveFee(int tier) => Money.FromYuan((ResidencePlace(tier)==null?tier==HomeTier:ResidencePlace(tier)==Home) ? 0 : 500);
        public Money ResidencePurchaseDue(int tier, bool mortgage) => ResidencePrice(tier) + ResidenceMoveFee(tier);

        public string ResidenceBlocker(int tier, bool purchase, bool mortgage = false)
        {
            if (IsGameOver || tier < 0 || tier > 3) return "当前无法办理住宅交易";
            if (purchase && mortgage) return "住宅仅支持全款购买";
            if (purchase && tier == 0) return "城郊旧居仅提供租赁";
            if (HomePriceCents > 0) return tier == HomeTier ? "这是你的自有住宅" : "请先出售现有自住房，再更换住宅";
            if (!purchase && ResidencePlace(tier) == Home) return "当前已租住这里";
            var target = ResidencePlace(tier);
            if (target != null)
            {
                if(ResidentialTenants(target.Id).Length>0)return "已有NPC登记居住，不能直接搬入；可通过住宅地图购买出租产权";
                string access = DistrictAccessBlocker(target); if (access != null) return access;
            }
            if (!InAgency) return "请到房产中介办理";
            if (City.LayoutVersion >= 7 && target != null && !AgencyServes(CurrentPlace, target)) return "请到住宅所属片区的中介办理";
            Money due = purchase ? ResidencePurchaseDue(tier, mortgage) : HomeTierRentQuote(tier) + ResidenceMoveFee(tier);
            return Ledger.Balance < due ? "现金不足，本次需要 " + due : null;
        }

        /// <summary>Buy a specific home in one transaction; moving does not require an intermediate rental.</summary>
        public bool BuyResidence(int tier, bool mortgage)
        {
            string why = ResidenceBlocker(tier, true, mortgage); if (why != null) return Fail(why);
            Money price = ResidencePrice(tier);
            if (!PayForResidenceMove(tier, "购置住宅", ResidencePurchaseDue(tier, mortgage), ResidenceName(tier) + (tier == HomeTier ? "" : "（含搬家费）"))) return false;
            HomePriceCents = price.Cents; HomeMortgageCents = 0;
            _residentialHomeId = null;
            HomeMonthlyPrincipalCents = 0;
            Say("已全款购入" + HomeTitle + "，家具与储物保留，不再支付月租；月维护费为房价0.1%。");
            return true;
        }

        private bool PayForResidenceMove(int tier, string category, Money due, string memo, LedgerAttribution attribution = null)
        {
            var room = RoomSpec.ForResidence(tier);
            if (room == ResidenceRoom) return Ledger.TryDebit(Clock.Today, category, due, memo, attribution);

            var layout = new List<PlacedFurniture>();
            var packed = new List<PlacedFurniture>();
            // Keep the cold storage usable when moving to a smaller residence.
            foreach (var item in _homeLayout.OrderByDescending(p => FurnitureCatalog.Get(p.Id)?.Role == FurnitureRole.Fridge))
            {
                var def = FurnitureCatalog.Get(item.Id);
                if (def == null || RoomPlanner.Check(room, layout, def, item, -1, null) == null)
                    layout.Add(item);
                else if (FindResidenceFurnitureSpot(room, layout, def, item) is PlacedFurniture replacement)
                    layout.Add(replacement);
                else
                    packed.Add(item);
            }
            if (HomeFridge.WeightKg > FridgeCapacityOf(layout) + 1e-9)
                return Fail("新住处放不下足够的冰箱。请先取出部分冷藏食品，再办理搬家。");
            if (!Ledger.TryDebit(Clock.Today, category, due, memo, attribution)) return false;

            _homeLayout.Clear();
            _homeLayout.AddRange(layout);
            foreach (var item in packed) HomeBox.AddFurniture(item.Id, 1);
            HomeTier = tier;
            if (packed.Count > 0) Say($"有 {packed.Count} 件家具放不下，已收进家中后仓。");
            return true;
        }

        private static PlacedFurniture FindResidenceFurnitureSpot(RoomSpec room, List<PlacedFurniture> layout,
            FurnitureDef def, PlacedFurniture item)
        {
            var at = RoomPlanner.Snap(room, def, item.X, item.Z, item.Rot);
            if (RoomPlanner.Check(room, layout, def, at, -1, null) != null)
            {
                bool wall = def.Slot == FurnitureSlot.Wall;
                double maxX = wall && item.Rot == 1 ? 0 : room.Width;
                double maxZ = wall && item.Rot != 1 ? 0 : room.Depth;
                at = null;
                for (double x = 0; x <= maxX && at == null; x += RoomPlanner.Grid)
                for (double z = 0; z <= maxZ && at == null; z += RoomPlanner.Grid)
                {
                    var candidate = RoomPlanner.Snap(room, def, x, z, item.Rot);
                    if (RoomPlanner.Check(room, layout, def, candidate, -1, null) == null) at = candidate;
                }
            }
            if (at == null) return null;
            var moved = item.Clone();
            moved.X = at.X;
            moved.Z = at.Z;
            moved.Rot = at.Rot;
            return moved;
        }
    }
}
