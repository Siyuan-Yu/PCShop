using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    [Serializable]
    public sealed class SaveResidentialLease
    {
        public string placeId;
        public long monthlyRentCents;
        public long weeklyRentCents;
        public int signedDay;
    }

    public sealed partial class SaveData
    {
        public string residentialHomeId;
        public SaveResidentialLease[] residentialLeases;
    }

    public sealed partial class GameSession
    {
        private string _residentialHomeId;
        private readonly Dictionary<string, (Money rent, int signedDay)> _additionalResidentialLeases =
            new Dictionary<string, (Money rent, int signedDay)>();
        public string CityHomeShortcut { get; private set; }
        public void SetCityHomeShortcut(Place place)
        {
            if (CanSleepAt(place)) CityHomeShortcut = place.Id;
        }

        public Place[] ResidentialPlaces => City.Places.Where(p=>p.Kind==PlaceKind.Home).ToArray();
        public IEnumerable<Place> RentedResidences
        {
            get
            {
                if (HomePriceCents <= 0) yield return Home;
                foreach (string id in _additionalResidentialLeases.Keys) yield return City.GetPlace(id);
            }
        }

        public bool HasResidentialLease(Place place) => place?.Kind == PlaceKind.Home &&
            (place == Home ? HomePriceCents <= 0 : _additionalResidentialLeases.ContainsKey(place.Id));

        public Money SignedResidentialRent(Place place) =>
            place == Home ? CurrentHomeRent : _additionalResidentialLeases.TryGetValue(place.Id, out var lease) ? lease.rent : Money.Zero;

        private int ResidentialLeaseSignedDay(Place place) =>
            place == Home ? _homeLeaseSignedDay : _additionalResidentialLeases[place.Id].signedDay;

        private static bool WeeklyRentDue(int day, int signedDay) =>
            day > signedDay && (day - signedDay) % GameDate.DaysPerWeek == 0;

        public int? NextResidentialRentDay(Place place)
        {
            if (!HasResidentialLease(place)) return null;
            int signed = ResidentialLeaseSignedDay(place);
            return signed + ((Clock.Today.TotalDays - signed) / GameDate.DaysPerWeek + 1) * GameDate.DaysPerWeek;
        }

        public bool CanSleepAt(Place place)
        {
            if (place == null || DistrictAccessBlocker(place) != null) return false;
            return place == Home || (place.Kind == PlaceKind.Home && (HasResidentialLease(place) || PropertyAt(place.Id)?.UseMode == "vacant")
                && !_cityPeople.Values.Any(p => p.HomePlaceId == place.Id && p.PersonId != CityLifeCatalog.InvitationTestPersonId));
        }

        private string ResidentialAgencyBlocker(string id)
        {
            if (IsGameOver || !City.HasPlace(id) || City.GetPlace(id).Kind != PlaceKind.Home) return "该地点不是住宅房源";
            var place = City.GetPlace(id);
            return DistrictAccessBlocker(place) ??
                (InAgency && AgencyServes(CurrentPlace, place) ? null : "请到住宅所属片区的中介办理");
        }

        public string ResidentialLeaseBlocker(string id)
        {
            string why = ResidentialAgencyBlocker(id);
            if (why != null) return why;
            var place = City.GetPlace(id);
            if (HasResidentialLease(place)) return "你已租下这套住宅";
            if (place == Home || PropertyAt(id) != null) return "你已拥有这套住宅";
            if (ResidentialTenants(id).Length > 0) return "已有住户，不能租赁";
            var rent = ResidentialRent(place);
            return Ledger.Balance < rent ? "现金不足，首周租金 " + rent : null;
        }

        public bool SignResidentialLease(string id)
        {
            string why = ResidentialLeaseBlocker(id);
            if (why != null) return Fail(why);
            var place = City.GetPlace(id);
            Money rent = ResidentialRent(place);
            int day = Clock.Today.TotalDays;
            var attribution = new LedgerAttribution(LedgerBusinessKind.Personal, propertyId: place.Id, transactionKind: "住宅租金");
            if (!Ledger.TryDebit(Clock.Today, "房租", rent, place.Name + "首周租金", attribution)) return false;
            _additionalResidentialLeases.Add(place.Id, (rent, day));
            Say($"已租下{place.Name}，可随时前往休息。");
            return true;
        }

        public Place NextResidenceAfterEndingLease(Place place)
        {
            if (place != Home) return Home;
            if (City.HasPlace(CityHomeShortcut))
            {
                var preferred = City.GetPlace(CityHomeShortcut);
                if (preferred != place && CanSleepAt(preferred)) return preferred;
            }
            return RentedResidences.FirstOrDefault(p => p != place && CanSleepAt(p)) ??
                _properties.Where(property => property.UseMode == "vacant" && City.HasPlace(property.PlaceId))
                    .Select(property => City.GetPlace(property.PlaceId)).FirstOrDefault(p => p != place && CanSleepAt(p));
        }

        public string EndResidentialLeaseBlocker(string id)
        {
            string why = ResidentialAgencyBlocker(id);
            if (why != null) return why;
            var place = City.GetPlace(id);
            if (!HasResidentialLease(place)) return "你没有租下这套住宅";
            return NextResidenceAfterEndingLease(place) == null ? "至少保留一处可入住住宅，请先租下或买下一套" : null;
        }

        public bool EndResidentialLease(string id)
        {
            string why = EndResidentialLeaseBlocker(id);
            if (why != null) return Fail(why);
            var place = City.GetPlace(id);
            if (place == Home)
            {
                var next = NextResidenceAfterEndingLease(place);
                var rent = SignedResidentialRent(next);
                int signedDay = HasResidentialLease(next) ? ResidentialLeaseSignedDay(next) : Clock.Today.TotalDays;
                var property = PropertyAt(next.Id);
                if (!PayForResidenceMove(ResidentialTier(next), "房租", Money.Zero, next.Name, allowPackedFridge: true)) return false;
                _homeLeaseRentCents = rent.Cents;
                _homeLeaseSignedDay = signedDay;
                _additionalResidentialLeases.Remove(next.Id);
                _residentialHomeId = next.Id;
                HomePriceCents = property?.PriceCents ?? 0;
                HomeMortgageCents = HomeMonthlyPrincipalCents = 0;
                if (property != null) _properties.Remove(property);
            }
            _additionalResidentialLeases.Remove(place.Id);
            if (CityHomeShortcut == place.Id) CityHomeShortcut = Home.Id;
            Say($"已退租{place.Name}。默认住处：{HomeTitle}。");
            return true;
        }

        private void ChargeResidentialRent(GameDate today)
        {
            var charges = new List<string>();
            Money total = Money.Zero;
            foreach (var place in ResidentialRentsDue(today.TotalDays))
            {
                var rent = SignedResidentialRent(place);
                var attribution = ResidentialRentAttribution(place.Id, today.TotalDays);
                if (!rent.IsPositive || Ledger.HasSource(attribution.SourceEventId)) continue;
                string name = place == Home ? HomeTitle : place.Name;
                var paid = ChargeRecordedBill("rent-home", place.Id, ResidentialLeaseSignedDay(place), attribution.SourceEventId, rent, "房租", name + "周租", attribution);
                total += paid;
                charges.Add($"{name} {paid}");
            }
            if (charges.Count > 0) Popup?.Invoke("住宅周租已扣款", string.Join("\n", charges) + $"\n合计 {total}");
        }

        private IEnumerable<Place> ResidentialRentsDue(int day) =>
            RentedResidences.Where(place => WeeklyRentDue(day, ResidentialLeaseSignedDay(place)));

        private static LedgerAttribution ResidentialRentAttribution(string placeId, int day) =>
            new LedgerAttribution(LedgerBusinessKind.Personal, propertyId: placeId,
                sourceEventId: "residential:" + placeId + ":rent:" + day, transactionKind: "住宅租金");

        private SaveResidentialLease[] SaveResidentialLeases() => _additionalResidentialLeases
            .Select(lease => new SaveResidentialLease { placeId = lease.Key, weeklyRentCents = lease.Value.rent.Cents, signedDay = lease.Value.signedDay }).ToArray();

        private void LoadResidentialLeases(SaveData data)
        {
            _additionalResidentialLeases.Clear();
            foreach (var lease in data.residentialLeases ?? Array.Empty<SaveResidentialLease>())
            {
                if (lease == null || !City.HasPlace(lease.placeId)) continue;
                var place = City.GetPlace(lease.placeId);
                if (place.Kind != PlaceKind.Home || place == Home || PropertyAt(place.Id) != null) continue;
                bool weekly = data.residentialWeeklyRentVersion > 0;
                Money rent = place == _initialHome ? HomeRent
                    : weekly ? Money.FromCents(lease.weeklyRentCents) : WeeklyResidentialRent(Money.FromCents(lease.monthlyRentCents));
                if (rent.IsPositive)
                    _additionalResidentialLeases[place.Id] = (rent, weekly ? Math.Max(0, Math.Min(lease.signedDay, Clock.Today.TotalDays)) : Clock.Today.TotalDays);
            }
        }

        public string SecondaryResidenceBed(Place place) =>
            place.Tier >= 2 ? "bed_good" : place.Tier == 1 ? "bed_spring" : PCShopSell.Core.Home.FurnitureCatalog.BedBoard;
        public CityPersonState[] ResidentialTenants(string id) => _cityPeople.Values.Where(p=>p.PersonId!=CityLifeCatalog.InvitationTestPersonId&&p.HomePlaceId==id).Select(p=>p.Copy()).ToArray();
        public int ResidentialTier(Place place) => Math.Max(0,Math.Min(3,place.Tier));
        public Money ResidentialRent(Place place) => place == _initialHome ? HomeRent : ResidentialMarketRent(ResidentialTier(place));
        public Money ResidentialPrice(Place place) => ResidentialTier(place)==0?(HomeBaseRent(0)*120).Scale(HomeSaleFactor):ResidencePrice(ResidentialTier(place));
        public Money ResidentialPurchaseDue(string id,bool mortgage,bool selfUse,bool keepCurrentResidence=false)
        {
            var place=City.GetPlace(id);var price=ResidentialPrice(place);
            return price+(selfUse?Money.FromYuan(place==Home||keepCurrentResidence?0:500):price.Scale(.02));
        }
        public string ResidentialTradeBlocker(string id,bool purchase,bool mortgage=false,bool selfUse=true,bool keepCurrentResidence=false)
        {
            string why=ResidentialAgencyBlocker(id);if(why!=null)return why;
            if(purchase&&mortgage)return "住宅仅支持全款购买";
            var place=City.GetPlace(id);
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
                if(HomePriceCents>0&&(!keepCurrentResidence||place==Home))return place==Home?"这是你的自有住宅":"请先出售当前自住房，再更换自住住宅";
                if(!purchase&&place==Home)return "当前已租住这里";
                if(owned!=null&&owned.UseMode=="managed")return "住宅已有租约，不能直接搬入";
                if(purchase&&owned!=null)return "已持有产权，请选择搬入自有空房";
            }
            Money due=!selfUse||purchase?ResidentialPurchaseDue(id,mortgage,selfUse,keepCurrentResidence)
                :Money.FromYuan(500)+(owned==null?ResidentialRent(place):Money.Zero);
            return Ledger.Balance<due?"现金不足，本次需要 "+due:null;
        }
        public bool RentResidentialHome(string id)
        {
            string why=ResidentialTradeBlocker(id,false);if(why!=null)return Fail(why);
            var place=City.GetPlace(id);var owned=PropertyAt(id);
            Money due=Money.FromYuan(500)+(owned==null?ResidentialRent(place):Money.Zero);
            if(!PayForResidenceMove(ResidentialTier(place),"搬家",due,owned==null?place.Name+"首周租金及搬家费":place.Name+"搬家费"))return false;
            _additionalResidentialLeases.Remove(place.Id);
            _residentialHomeId=place.Id;
            _homeLeaseRentCents=owned==null?ResidentialRent(place).Cents:0;
            _homeLeaseSignedDay=Clock.Today.TotalDays;
            if(owned!=null)
            {
                HomePriceCents=owned.PriceCents;HomeMortgageCents=HomeMonthlyPrincipalCents=0;
                _properties.Remove(owned);
            }
            Say("已搬入"+HomeTitle+"，家具、食品和储物保留。");return true;
        }
        public bool BuyResidentialHome(string id,bool mortgage,bool selfUse,bool keepCurrentResidence=false)
        {
            string why=ResidentialTradeBlocker(id,true,mortgage,selfUse,keepCurrentResidence);if(why!=null)return Fail(why);
            var place=City.GetPlace(id);var price=ResidentialPrice(place);
            var due=ResidentialPurchaseDue(id,mortgage,selfUse,keepCurrentResidence);
            var attribution=LedgerAttribution.ForAsset(id,"residential:purchase:"+id+":"+Clock.Now.TotalMinutes);
            if(selfUse&&(!keepCurrentResidence||place==Home))
            {
                bool sameHome=place==Home;
                if(!PayForResidenceMove(ResidentialTier(place),"购置住宅",due,place.Name,attribution))return false;
                _residentialHomeId=place.Id;HomePriceCents=price.Cents;HomeMortgageCents=HomeMonthlyPrincipalCents=0;
                Say(sameHome?"已购入"+HomeTitle+"，该住宅不再缴周租。":"已购入并搬入"+HomeTitle+"，不再缴周租；家具与储物保留。");
            }
            else
            {
                if(!Ledger.TryDebit(Clock.Today,selfUse?"购置住宅":"购置出租住宅",due,place.Name,attribution))return false;
                var tenants=ResidentialTenants(id);
                _properties.Add(new OwnedProperty{PlaceId=id,PriceCents=price.Cents,
                    BoughtDay=Clock.Today.TotalDays,CitySchema=1,UseMode=tenants.Length>0?"npc":"vacant",TenantPersonId=tenants.FirstOrDefault()?.PersonId,
                    TenantName=string.Join("、",tenants.Select(p=>CityPerson(p.PersonId)?.Name??p.PersonId)),WeeklyRentCents=ResidentialRent(place).Cents,
                    LeaseEndDay=Clock.Today.TotalDays+360,LastSettledMonth=Clock.Today.TotalDays/30});
                Say(tenants.Length>0?"已带住户购入住宅产权，七天后开始每周收租。":"已购入住宅，可随时前往休息。");
            }
            _additionalResidentialLeases.Remove(place.Id);
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
