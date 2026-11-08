using System;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Shop;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        private const string ExpoAcceptanceFlag = "debug:brand-expo-acceptance";
        public bool IsBrandExpoAcceptance
        {
            get
            {
#if UNITY_EDITOR
                return IsCityLifeTest && Flags.Contains(ExpoAcceptanceFlag);
#else
                return false;
#endif
            }
        }

#if UNITY_EDITOR
        private const string BrandCreationSalesMemo = "品牌创建测试档历史营收";

        public bool IsBrandCreationAcceptance => IsCityLifeTest && Ledger.Entries.Any(entry => entry.Memo == BrandCreationSalesMemo);

        public bool DebugPrepareBrandAcceptanceStock()
        {
            if (!IsBrandCreationAcceptance && !IsBrandExpoAcceptance) return Fail("仅用于品牌或展会测试档");
            foreach (var shop in MyShops.Where(s => s.Rented))
            {
                if (shop.RetailKind == "computer")
                {
                    foreach (string id in new[] { "cpu0", "gpu0", "ram0" })
                    {
                        var part = PartCatalog.Get(id);
                        int missing = Math.Max(0, 100 - shop.Sim.Stock.Count(part, true));
                        if (missing > 0) shop.Sim.Stock.ForceAdd(part, true, missing);
                    }
                }
                else
                {
                    var products = shop.RetailKind == "models" ? ModelProducts.Regular.Select(p => p.Id)
                        : shop.RetailKind == "clothing" ? ClothingProducts.All.Select(p => p.Id) : Array.Empty<string>();
                    foreach (string id in products.Take(3))
                    {
                        int missing = Math.Max(0, 100 - shop.Sim.Stock.GoodsCount(id));
                        if (missing > 0) shop.Sim.Stock.ForceAddGoods(id, missing);
                    }
                }
            }
            Say("测试门店库存已补齐：电脑、服装、模型店各3种同业商品，每种至少100件，已有更多库存保留。");
            return true;
        }

        public string BrandCreationSuburbHomeBlocker
        {
            get
            {
                if (!IsBrandCreationAcceptance) return "仅用于品牌创建测试档";
                if (CanSleepAt(_initialHome)) return "城郊住处已可使用";
                if (PropertyAt(_initialHome.Id) != null || ResidentialTenants(_initialHome.Id).Length > 0)
                    return "城郊住处已有租约或住户";
                return null;
            }
        }

        public bool DebugRestoreBrandCreationSuburbHome()
        {
            string why = BrandCreationSuburbHomeBlocker;
            if (why != null) return Fail(why);

            _properties.Add(new OwnedProperty
            {
                PlaceId = _initialHome.Id,
                PriceCents = ResidentialPrice(_initialHome).Cents,
                WeeklyRentCents = ResidentialRent(_initialHome).Cents,
                BoughtDay = BrandDay,
                CitySchema = 1,
                UseMode = "vacant",
                LastSettledMonth = BrandDay / 30
            });
            Say("城郊住处已加入当前测试档，可从底栏住宅下拉选择。");
            return true;
        }

        private void PrepareBrandCreationAcceptance(int industryCount)
        {
            _district = new DistrictProgress { UnlockedTier = 1, Chapter = 1 };
            _district.Completed = DistrictNodeChains[0].ToArray();
            _district.CommunityMapDelivered = true;
            TopUpExpoAcceptanceCash();

            var available = RentableUnits().Where(p => ShopAt(p) == null && PropertyAt(p.Id) == null && VentureAt(p.Id) == null).ToArray();
            var suburb = new System.Collections.Generic.Queue<Place>(available.Where(p => DistrictTierOf(p) == 0));
            var community = new System.Collections.Generic.Queue<Place>(available.Where(p => DistrictTierOf(p) == 1));
            bool useMainShop = DistrictTierOf(MainShop.Place) == 0;
            if (suburb.Count < 3 * industryCount - (useMainShop ? 1 : 0) || community.Count < 2 * industryCount)
                throw new InvalidOperationException($"品牌创建验收档的可用铺位不足（城郊 {suburb.Count}，社区 {community.Count}）");

            var kinds = BrandKinds.Take(industryCount).ToArray();
            foreach (string kind in kinds)
            {
                for (int index = 0; index < 5; index++)
                {
                    bool inSuburb = index < 3;
                    if (kind == "computer" && index == 0 && useMainShop) SetFocus(MainShop);
                    else
                    {
                        var place = (inSuburb ? suburb : community).Dequeue();
                        var rent = ShopRentQuote(place);
                        PaySigning(rent, place);
                        OpenNewShop(place, rent, RealEstate.Deposit(rent));
                    }
                    var shop = CurrentShop;
                    shop.RetailKind = shop.Sim.RetailKind = kind;
                    FitStarterShop();
                    shop.RenovationDay = BrandDay - 31;
                    Flags.Remove(OpeningChoicePrefix + shop.Place.Id);
                    long dailySales = inSuburb ? BrandRivalMonthlySales(kind, BrandDay) / 60 : 100000;
                    for (int day = BrandDay - 30; day < BrandDay; day++)
                        Ledger.Credit(new Time.GameDate(day), "零售", Money.FromCents(dailySales),
                            BrandCreationSalesMemo, Finance.LedgerAttribution.ForShop(shop.Place.Id));
                }
            }
            _brands = new BrandBusinessState
            {
                LastDay = BrandDay,
                Qualifications = kinds.Select(kind => new BrandQualification
                    { Kind = kind, LeadingDays = 14, CommunityDays = 30 }).ToArray()
            };
            DebugPrepareBrandAcceptanceStock();
            SetFocus(MainShop);

            var residence = ResidencePlace(1);
            var agency = residence == null ? null : City.Places.FirstOrDefault(p => p.Kind == PlaceKind.Agency && AgencyServes(p, residence));
            if (agency == null) throw new InvalidOperationException("品牌创建验收档找不到社区住宅或所属中介");
            var previousPlace = CurrentPlace;
            try
            {
                CurrentPlace = agency;
                if (!RentResidentialHome(residence.Id))
                    throw new InvalidOperationException("品牌创建验收档租住社区住宅失败");
            }
            finally { CurrentPlace = previousPlace; }

            if (!DebugRestoreBrandCreationSuburbHome())
                throw new InvalidOperationException("品牌创建验收档无法保留城郊住处");

            PlaceAcquiredMotor(NewMotor(MotorCatalog.Legacy(VehicleKind.EBike).Id), true, null);

            if (!SetVehicle(VehicleKind.EBike))
                throw new InvalidOperationException("品牌创建验收档无法启用小电驴");
            RefreshBrandTraffic();
        }

        public bool DebugExpoFastForwarding { get; private set; }
        private void PrepareQuarterlyExpoAcceptance()
        {
            PrepareBrandExpoAcceptance();DebugAddCash(1350000);_brands.Brands[0].Influence=320;
            if(!DebugPrepareExpoBusinessBase())throw new InvalidOperationException("季度展会验收经营基础准备失败");
            foreach(var talent in KolMarket.All)Meet(talent.Id,25);
            foreach(var part in PartCatalog.All.Where(p=>p.Id!="cpu0"&&p.Id!="gpu0").Take(6).Concat(new[]{PartCatalog.Get("cpu0"),PartCatalog.Get("gpu0")}))MainShop.Sim.Stock.ForceAdd(part,true,Math.Max(0,150-MainShop.Sim.Stock.Count(part,true)));
            Say("4天季度展会测试档：城郊5店、社区1店已开业并配备店员，住在社区；8种展品各150件，现金150万，知名度320。请亲自升级品牌、签约KOL、预订摊位并委托海报。");
        }

        public string DebugExpoBusinessBaseBlocker => !IsBrandExpoAcceptance?"仅用于独立展会验收档":
            IsGameOver||IsWorking||_sleeping||CityActivityExclusive||BrandExpoExclusive||Trip!=null?"请先结束当前工作、活动、参展或出行":
            HomePriceCents>0&&DistrictTierOf(Home)!=1?"当前有其他片区的自住房，请先在中介处理后再补齐社区住处":null;

        public bool DebugExpoBusinessBaseReady => IsBrandExpoAcceptance&&DistrictPrerequisiteBlocker(1)==null
            &&DistrictOperatingShops(1)>=1&&DistrictTierOf(Home)==1;

        // Construct the same leased shops, employees and home used by normal play.
        // This is deliberately opt-in for old test saves and never runs on loading a save.
        public bool DebugPrepareExpoBusinessBase()
        {
            string why=DebugExpoBusinessBaseBlocker;if(why!=null)return Fail(why);
            var available=RentableUnits().Where(p=>ShopAt(p)==null&&PropertyAt(p.Id)==null&&VentureAt(p.Id)==null)
                .OrderBy(p=>p.Business==RealEstate.VacantBusiness?0:p.Id.StartsWith("shop",StringComparison.Ordinal)?1:2).ThenBy(p=>p.Id,StringComparer.Ordinal).ToArray();
            int suburbNeed=Math.Max(0,5-DistrictOperatingShops(0)),communityNeed=Math.Max(0,1-DistrictOperatingShops(1));
            var additions=available.Where(p=>DistrictTierOf(p)==0).Take(suburbNeed)
                .Concat(available.Where(p=>DistrictTierOf(p)==1).Take(communityNeed)).ToArray();
            var residence=ResidencePlace(1);
            var agency=City.Places.FirstOrDefault(p=>p.Kind==PlaceKind.Agency&&AgencyServes(p,residence));
            if(additions.Length!=suburbNeed+communityNeed)return Fail("可用铺位不足，未修改验收档");
            if(DistrictTierOf(Home)!=1&&(residence==null||agency==null||ResidentialTenants(residence.Id).Length>0))return Fail("社区空置住宅不可用，未修改验收档");

            var keep=_focus;var placeBefore=CurrentPlace;
            try
            {
                TopUpExpoAcceptanceCash();
                int n=0;string[] staffNames={"邵文清","陶知远","季以安","贺书宁","秦悦然","沈沐言"};
                foreach(var place in additions)
                {
                    var rent=ShopRentQuote(place);PaySigning(rent,place);OpenNewShop(place,rent,RealEstate.Deposit(rent));
                    var shop=CurrentShop;shop.RetailKind=shop.Sim.RetailKind="computer";
                    FitStarterShop();shop.RenovationDay=-1;Flags.Remove(OpeningChoicePrefix+place.Id);
                    foreach(string id in new[]{"cpu0","gpu0","ram0","ssd0"})shop.Sim.Stock.ForceAdd(PartCatalog.Get(id),true,30);
                    shop.Employee=new Employee{Id="expo_cashier_"+place.Id,Name=staffNames[n++%staffNames.Length],DailyWageYuan=150,CheckoutMinutes=2};
                    EnsureCityEmployee(shop.Employee);var person=CityPersonState(shop.Employee.PersonId);
                    _employeeHireShops[shop.Employee.Id]=place.Id;EverHired=true;
                    var stand=StandOf(place);person.WorkPlaceId=person.LocationPlaceId=place.Id;person.X=stand.X;person.Y=stand.Y;
                    person.Task=CityPersonTask.Working;person.TaskLabel="门店收银";person.Route=null;Meet(person.PersonId,25);
                    ApplyUpgrades();
                }
                if(DistrictTierOf(Home)!=1)
                {
                    CurrentPlace=agency;
                    if(!RentResidentialHome(residence.Id))throw new InvalidOperationException("社区住宅租住未完成");
                }
                var brand=_brands.Brands.FirstOrDefault(b=>b.Id=="acceptance-expo");
                if(brand!=null)brand.ShopIds=brand.ShopIds.Concat(MyShops.Where(s=>s.RetailKind==brand.Kind&&DistrictTierOf(s.Place)<=1).Select(s=>s.Place.Id)).Distinct().ToArray();
                // The entrance story is background of this explicit cheat preset. Later chapters remain untouched.
                _district.Completed=_district.Completed.Union(DistrictNodeChains[0]).ToArray();
                _district.CommunityMapQuest=true;_district.CommunitySearchVersion=2;_district.CommunityMapDelivered=true;
                DebugPrepareBrandAcceptanceStock();
                TopUpExpoAcceptanceCash();RefreshBrandTraffic();
            }
            finally{CurrentPlace=placeBefore;SetFocus(keep);}
            Say("验收经营基础已补齐：城郊"+DistrictOperatingShops(0)+"家、社区"+DistrictOperatingShops(1)+"家开业门店；社区住处"+HomeTitle+"，周租"+CurrentHomeRent+"。租约、库存、工资与房租按正常系统继续结算。");
            return true;
        }

        private void TopUpExpoAcceptanceCash()
        {
            long missing=150000000-Ledger.Balance.Cents;
            if(missing>0)Ledger.Credit(Clock.Today,"作弊",Money.FromCents(missing),"展会验收经营基础资金补足至150万元");
        }

        private void PrepareKolLiveAcceptance()
        {
            PrepareBrandExpoAcceptance();
            Flags.Remove(ExpoAcceptanceFlag);
            Clock.Restore(Clock.Today,17*60+55);
            DebugAddCash(350000);
            _brands.Brands[0].Influence=320;
            foreach(var talent in KolMarket.All)Meet(talent.Id,25);
            foreach(string id in new[]{"momo","ake","qiao"})
            {
                var p=CityPersonState(id);var stand=StandOf(City.GetPlace(CommunityMcnId));
                p.Route=null;p.LocationPlaceId=CommunityMcnId;p.X=stand.X;p.Y=stand.Y;p.ActivityId=null;p.VisitId=null;
            }
            RefreshBrandTraffic();
            Say("KOL直播独立测试档：17:55，社区与品牌已开放，15位KOL已认识；初级名额3人，可支付现金升级。请亲自签约、排班，18:00开始观看直播。未预签约或预发收益。");
        }

        private void PrepareBrandExpoAcceptance()
        {
            if (!IsCityLifeTest || !City.IsLakeCity) throw new InvalidOperationException("品牌展会预置需要独立湖城测试档");
            Flags.Add(ExpoAcceptanceFlag);
            _district = new DistrictProgress { UnlockedTier = 1, Chapter = 1 };
            SetSmartphone(true);
            MainShop.RenovationDay = -1;
            MainShop.RetailKind = MainShop.Sim.RetailKind = "computer";
            foreach (string id in new[] { "cpu0", "gpu0" })
            {
                var part = PartCatalog.Get(id);
                MainShop.Sim.Stock.ForceAdd(part, true, Math.Max(0, 60 - MainShop.Sim.Stock.Count(part, true)));
            }
            _brands = new BrandBusinessState { Brands = new[] { new BrandRecord {
                Id = "acceptance-expo", Name = "晨湾科技", Kind = "computer", Position = "value", ShopIds = new[] { MainShop.Place.Id }
            } } };
            foreach (string person in BrandPeople) Meet(person, 25);
            var employee = new Employee { Id = "expo_acceptance_cashier", PersonId = "life_ming", Name = "许明", DailyWageYuan = 150, CheckoutMinutes = 2 };
            MainShop.Employee = employee;
            var state = CityPersonState(employee.PersonId);
            var point = StandOf(MainShop.Place);
            state.WorkPlaceId = state.LocationPlaceId = MainShop.Place.Id;
            state.X = point.X; state.Y = point.Y; state.Task = CityPersonTask.Working; state.Route = null;
            Meet(employee.PersonId, 25);
            ApplyUpgrades();
            Leave(); ArriveAtNode(City.GetPlace("life_expo").NodeId);
            Say("品牌展会独立测试档：社区已开放，品牌、样品和员工已准备。先在季度展会页报名；之后可从作弊窗口推进到开幕。接待和订单仍需实际完成。");
        }

        public string DebugExpoOpeningBlocker
        {
            get
            {
                if (!IsBrandExpoAcceptance) return "请先创建品牌展会独立测试档";
                if (IsGameOver || IsWorking || _sleeping || CityActivityExclusive || Trip != null) return "请先结束当前工作、出行或活动";
                var exhibit = _brands.Exhibits.Where(e => !e.Finished && !e.Missed && e.Day >= BrandDay).OrderBy(e => e.Day).FirstOrDefault();
                if (exhibit == null) return "请先在季度展会页完成报名";
                return Clock.Now.TotalMinutes >= exhibit.Day * 1440L + 540 ? "已到开幕时间，请进入会展中心接待" : null;
            }
        }

        public bool DebugAdvanceToExpoOpening()
        {
            string why = DebugExpoOpeningBlocker;
            if (why != null) return Fail(why);
            var exhibit = _brands.Exhibits.Where(e => !e.Finished && !e.Missed && e.Day >= BrandDay).OrderBy(e => e.Day).First();
            long target = exhibit.Day * 1440L + 540;
            // Advance normal business/NPC/transport systems. Only body needs are
            // replenished during this explicit test shortcut, avoiding a week-long faint loop.
            DebugExpoFastForwarding = true;
            try
            {
                while (Clock.Now.TotalMinutes < target && !IsGameOver)
                {
                    Player.SleepDebt = 0; Player.Energy = Player.MaxEnergy; Player.Hunger = Player.Health = 100;
                    long before = Clock.Now.TotalMinutes;
                    AdvanceClock((int)Math.Min(60, target - before));
                    if (Clock.Now.TotalMinutes <= before) return Fail("时间推进被打断，请检查当前事件");
                }
            }
            finally { DebugExpoFastForwarding = false; }
            Player.SleepDebt = 0; Player.Energy = Player.MaxEnergy; Player.Hunger = Player.Health = 100;
            Say("已推进到展会当天09:00。期间经营、工资和人物路线正常结算，身体状态由验收工具补足；请实际进入会展中心接待。");
            return Clock.Now.TotalMinutes == target;
        }
#endif
    }
}
