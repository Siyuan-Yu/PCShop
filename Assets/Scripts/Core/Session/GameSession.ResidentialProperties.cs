using System;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    public sealed partial class SaveData { public string residentialHomeId; }
    public sealed partial class GameSession
    {
        private string _residentialHomeId;
        public string CityHomeShortcut { get; private set; }
        public void SetCityHomeShortcut(Place place)
        {
            if (CanSleepAt(place)) CityHomeShortcut = place.Id;
        }

        public Place[] ResidentialPlaces => City.Places.Where(p=>p.Kind==PlaceKind.Home).ToArray();
        public bool CanSleepAt(Place place)
        {
            if (place == null || DistrictAccessBlocker(place) != null) return false;
            return place == Home || (place.Kind == PlaceKind.Home && PropertyAt(place.Id)?.UseMode == "vacant"
                && !_cityPeople.Values.Any(p => p.HomePlaceId == place.Id && p.PersonId != CityLifeCatalog.InvitationTestPersonId));
        }

        public string SecondaryResidenceBed(Place place) =>
            place.Tier >= 2 ? "bed_good" : place.Tier == 1 ? "bed_spring" : PCShopSell.Core.Home.FurnitureCatalog.BedBoard;
        public CityPersonState[] ResidentialTenants(string id) => _cityPeople.Values.Where(p=>p.PersonId!=CityLifeCatalog.InvitationTestPersonId&&p.HomePlaceId==id).Select(p=>p.Copy()).ToArray();
        public int ResidentialTier(Place place) => Math.Max(0,Math.Min(3,place.Tier));
        public Money ResidentialRent(Place place) => HomeTierRentQuote(ResidentialTier(place));
        public Money ResidentialPrice(Place place) => ResidentialTier(place)==0?(HomeBaseRent(0)*120).Scale(HomeSaleFactor):ResidencePrice(ResidentialTier(place));
        public Money ResidentialPurchaseDue(string id,bool mortgage,bool selfUse)
        {
            var place=City.GetPlace(id);var price=ResidentialPrice(place);
            return price+(selfUse?Money.FromYuan(place==Home?0:500):price.Scale(.02));
        }
        public string ResidentialTradeBlocker(string id,bool purchase,bool mortgage=false,bool selfUse=true)
        {
            if(IsGameOver||!City.HasPlace(id)||City.GetPlace(id).Kind!=PlaceKind.Home)return "该地点不是住宅房源";
            if(purchase&&mortgage)return "住宅仅支持全款购买";
            var place=City.GetPlace(id);string access=DistrictAccessBlocker(place);if(access!=null)return access;
            if(!InAgency||!AgencyServes(CurrentPlace,place))return "请到住宅所属片区的中介办理";
            var owned=PropertyAt(id);var tenants=ResidentialTenants(id);
            if(!selfUse)
            {
                if(!purchase)return "出租住宅产权需要购买";
                if(place==Home)return "当前住处请使用自住购买";
                if(owned!=null)return "已经持有这套住宅产权";
            }
            else
            {
                if(tenants.Length>0)return "已有NPC居住，不能租住或直接搬入；可以带住户购买产权";
                if(HomePriceCents>0)return place==Home?"这是你的自有住宅":"请先出售当前自住房，再更换自住住宅";
                if(!purchase&&place==Home)return "当前已租住这里";
                if(owned!=null&&owned.UseMode=="managed")return "住宅已有租约，不能直接搬入";
                if(purchase&&owned!=null)return "已持有产权，请选择搬入自有空房";
            }
            Money due=!selfUse||purchase?ResidentialPurchaseDue(id,mortgage,selfUse)
                :Money.FromYuan(500)+(owned==null?ResidentialRent(place):Money.Zero);
            return Ledger.Balance<due?"现金不足，本次需要 "+due:null;
        }
        public bool RentResidentialHome(string id)
        {
            string why=ResidentialTradeBlocker(id,false);if(why!=null)return Fail(why);
            var place=City.GetPlace(id);var owned=PropertyAt(id);
            Money due=Money.FromYuan(500)+(owned==null?ResidentialRent(place):Money.Zero);
            if(!Ledger.TryDebit(Clock.Today,"搬家",due,place.Name+"首月租金及搬家费"))return false;
            SetResidentialHome(place);
            _homeLeaseRentCents=owned==null?ResidentialRent(place).Cents:0;
            if(owned!=null)
            {
                HomePriceCents=owned.PriceCents;HomeMortgageCents=HomeMonthlyPrincipalCents=0;
                _properties.Remove(owned);
            }
            Say("已搬入"+HomeTitle+"，家具、食品和储物保留。");return true;
        }
        private void SetResidentialHome(Place place)
        {
            HomeTier=ResidentialTier(place);
            _residentialHomeId=place.Id;
        }
        public bool BuyResidentialHome(string id,bool mortgage,bool selfUse)
        {
            string why=ResidentialTradeBlocker(id,true,mortgage,selfUse);if(why!=null)return Fail(why);
            var place=City.GetPlace(id);var price=ResidentialPrice(place);
            if(!Ledger.TryDebit(Clock.Today,selfUse?"购置住宅":"购置出租住宅",ResidentialPurchaseDue(id,mortgage,selfUse),place.Name,
                LedgerAttribution.ForAsset(id,"residential:purchase:"+id+":"+Clock.Now.TotalMinutes)))return false;
            if(selfUse)
            {
                SetResidentialHome(place);HomePriceCents=price.Cents;HomeMortgageCents=HomeMonthlyPrincipalCents=0;
                Say("已购入并搬入"+HomeTitle+"，不再缴月租；家具与储物保留。");
            }
            else
            {
                var tenants=ResidentialTenants(id);
                _properties.Add(new OwnedProperty{PlaceId=id,PriceCents=price.Cents,
                    BoughtDay=Clock.Today.TotalDays,CitySchema=1,UseMode=tenants.Length>0?"npc":"vacant",TenantPersonId=tenants.FirstOrDefault()?.PersonId,
                    TenantName=string.Join("、",tenants.Select(p=>CityPerson(p.PersonId)?.Name??p.PersonId)),MonthlyRentCents=ResidentialRent(place).Cents,
                    LeaseEndDay=Clock.Today.TotalDays+360,LastSettledMonth=Clock.Today.TotalDays/30});
                Say(tenants.Length>0?"已带住户购入住宅产权。原NPC不搬走，从下个月账期起向你交租。":"已购入空置住宅。没有实际租户时不产生租金，可以搬入自住。");
            }
            return true;
        }
        public bool SellResidentialInvestment(string id)
        {
            var property=PropertyAt(id);
            if(property==null||!City.HasPlace(id)||City.GetPlace(id).Kind!=PlaceKind.Home||IsGameOver||!InAgency||!AgencyServes(CurrentPlace,City.GetPlace(id)))return Fail("请到住宅所属中介办理出售");
            Money net=PropertySaleNet(property);
            if(net.IsPositive)Ledger.Credit(Clock.Today,"住宅出售",net,City.GetPlace(id).Name,LedgerAttribution.ForAsset(id,"residential:sale:"+id+":"+Clock.Now.TotalMinutes));
            _properties.Remove(property);Say("产权已出售，原住户和住所、人物关系保留。");return true;
        }
    }
}
