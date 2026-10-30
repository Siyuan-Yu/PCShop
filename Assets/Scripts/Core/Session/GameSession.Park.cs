using System;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Shop;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        public string ParkPlaceId {get;private set;}
        public int ParkReadyDay {get;private set;}
        public bool HasPark => !string.IsNullOrEmpty(ParkPlaceId);
        private string ParkSlotId(int slot) => ParkPlaceId + "/unit/" + slot;
        public bool IsParkVenture(ManagedVenture v) => HasPark && v.PlaceId.StartsWith(ParkPlaceId+"/unit/",StringComparison.Ordinal);
        public ManagedVenture ParkUnit(int slot) => HasPark ? VentureAt(ParkSlotId(slot)) : null;
        public Place VentureLocation(ManagedVenture v) => City.GetPlace(IsParkVenture(v) ? ParkPlaceId : v.PlaceId);
        public string VentureLocationName(ManagedVenture v) => IsParkVenture(v) ? "电脑乐园 · "+(int.Parse(v.PlaceId.Substring(v.PlaceId.LastIndexOf('/')+1))+1)+" 号铺" : VentureLocation(v).Name;
        public Money ParkLandPrice(ShopListing l) => Money.Max(Money.FromYuan(300000),l.Rent*120);
        public string ParkPurchaseBlocker(ShopListing l)
        {
            if(HasPark) return "已拥有园区";
            if(!HasShop || PlayerShop.Reputation<35) return "需要主店和知名度 35";
            if(l==null || l.Size!=ShopSize.Large || l.OpenDay>Clock.Today.TotalDays || l.CloseDay<Clock.Today.TotalDays) return "需要一间当前空置的大型铺面";
            if(IsMyShop(City.GetPlace(l.PlaceId)) || VentureAt(l.PlaceId)!=null || PropertyAt(l.PlaceId)!=null) return "这处铺面已在使用";
            if(Ledger.Balance<ParkLandPrice(l)+Money.FromYuan(50000)) return "需全额地价和 ¥50,000 公共区域改造费";
            return null;
        }
        public bool BuyPark(string id)
        {
            if(IsGameOver || !InAgency) return Fail("在房产中介签园区购置合同。");
            var l=_listings.Find(x=>x.PlaceId==id); string why=ParkPurchaseBlocker(l);if(why!=null)return Fail(why);
            Money land=ParkLandPrice(l);
            Ledger.ForceDebit(Clock.Today,"园区投资",land+Money.FromYuan(50000),"购置及公共区域改造");
            _properties.Add(new OwnedProperty{PlaceId=id,PriceCents=land.Cents,BoughtDay=Clock.Today.TotalDays});
            ParkPlaceId=id;ParkReadyDay=Clock.Today.TotalDays+7;_vacant.Remove(id);_listings.Remove(l);
            Say("电脑乐园开始改造，7 天后开放 2×3 铺位。相邻的不同业态可相互导流；物业每月正常收维护费。");return true;
        }
        public double ParkTrafficBonus(ManagedVenture v)
        {
            if(!IsParkVenture(v))return 1;
            int slot=int.Parse(v.PlaceId.Substring(v.PlaceId.LastIndexOf('/')+1));int neighbors=0;
            for(int i=0;i<6;i++)
            {
                var other=ParkUnit(i);
                if(other==null || other.Paused || other.Kind==v.Kind)continue;
                if(Math.Abs(i%3-slot%3)+Math.Abs(i/3-slot/3)==1) neighbors++;
            }
            return 1+Math.Min(.24,neighbors*.08);
        }
        public string ParkUnitBlocker(int slot,string kind)
        {
            var d=Ventures.Get(kind);
            if(!HasPark || Clock.Today.TotalDays<ParkReadyDay)return "园区尚未完工";
            if(slot<0 || slot>=6 || ParkUnit(slot)!=null || d==null)return "请选择一个空铺位";
            if(PlayerShop.Reputation<d.MinReputation)return $"知名度需要 {d.MinReputation:0}";
            if(d.HardwareCount>0 && PartCatalog.BuildList(d.HardwareTier).Any(p=>PlayerShop.Stock.CountAll(p)<d.HardwareCount))return $"需要从主店调拨 {d.HardwareCount} 套{PartCatalog.TierName(d.HardwareTier)}配件";
            if(Ledger.Balance<Money.FromYuan(d.Capital+5000))return "装修及周转金不足";
            return null;
        }
        public bool OpenParkUnit(int slot,string kind)
        {
            if(IsGameOver)return false;string why=ParkUnitBlocker(slot,kind);if(why!=null)return Fail(why);
            var d=Ventures.Get(kind);Money capital=Money.FromYuan(d.Capital);
            if(d.HardwareCount>0)foreach(var p in PartCatalog.BuildList(d.HardwareTier))
            {capital+=Market.PriceOf(p)*d.HardwareCount;for(int i=0;i<d.HardwareCount;i++)PlayerShop.Stock.TakeOne(p);}
            Ledger.ForceDebit(Clock.Today,"园区投资",Money.FromYuan(d.Capital+5000),d.Name+"入驻");
            _ventures.Add(new ManagedVenture{PlaceId=ParkSlotId(slot),Kind=kind,OpenDay=Clock.Today.TotalDays,LastSettledDay=Clock.Today.TotalDays,CapitalCents=capital.Cents,ReserveCents=500000});
            Flags.Add("tutorial:park");
            Say(d.Name+"入驻园区，可在分店经营页管理周转金、定价与维护。");return true;
        }
        public bool MoveParkUnit(int from,int to)
        {
            if(IsGameOver || !HasPark || from<0 || from>=6 || to<0 || to>=6 || from==to)return false;
            var a=ParkUnit(from);var b=ParkUnit(to);if(a==null)return Fail("先选择一家已开业的铺位。");
            if(!Ledger.TryDebit(Clock.Today,"园区调整",Money.FromYuan(1000),"铺位搬迁/互换"))return Fail("布局调整需要 ¥1,000。");
            a.PlaceId=ParkSlotId(to);if(b!=null)b.PlaceId=ParkSlotId(from);return true;
        }
    }
}
