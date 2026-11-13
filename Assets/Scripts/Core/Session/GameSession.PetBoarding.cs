using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Home;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    public enum PetCareAction { Water, Litter, CatFood, DogFood }

    [Serializable]
    public sealed class PetStay
    {
        public long Id, RequestedAt;
        public long NextVisitAt;
        public string OwnerId, Name;
        public bool Dog;
        public int Coat;
        // Before shared care, these dates were recorded per pet.
        public int LastFedDay = -1, LastWaterDay = -1, LastLitterDay = -1;
        public int LastPlayDay = -1, Happiness = 55, LastPlayBonus;
        public int Weeks, PaidWeeks, Condition = 68;
        public int LastConditionDay = -1, LastKeeperDay = -1;
        public int WeeklyPrice => Dog ? 1800 : 1400;
    }

    [Serializable]
    public sealed class PetBoardingState
    {
        public string ShopId;
        public long NextOfferAt;
        public long NextStaffCheckAt;
        public int WaterDay = -1, LitterDay = -1, CatFoodDay = -1, DogFoodDay = -1;
        public int CatFoodTier, DogFoodTier;
        public PetStay Offer;
        public PetStay[] Stays = Array.Empty<PetStay>();
        public string LastPickup;
    }

    public sealed partial class SaveData
    {
        public PetBoardingState[] petBoarding;
        public int petCareVersion;
    }

    public sealed partial class GameSession
    {
        private const string PetOwnerVisitLabel = "到店办理宠物代养";
        public const int PetsPerKeeper = 2;
        private const int WeekMinutes = GameDate.DaysPerWeek * GameTimestamp.MinutesPerDay;
        private static readonly string[] PetNames = { "团子", "豆豆", "栗子", "阿福", "芝麻", "奶盖", "糯米", "布丁", "点点", "桃子", "雪球", "花卷" };
        private readonly Dictionary<string, PetBoardingState> _petBoarding = new Dictionary<string, PetBoardingState>();
        private readonly Dictionary<string, OwnedShop> _petOwnerVisits = new Dictionary<string, OwnedShop>();

        public bool IsPetShop(OwnedShop shop) => shop?.RetailKind == PetShopCatalog.ShopKind;
        public PetBoardingState PetBoarding(OwnedShop shop) => IsPetShop(shop) && _petBoarding.TryGetValue(shop.Place.Id, out var state) ? state : null;
        public IReadOnlyList<PetStay> PetGuests(OwnedShop shop) => PetBoarding(shop)?.Stays ?? Array.Empty<PetStay>();
        public PlacedFurniture PetHouse(OwnedShop shop) => IsPetShop(shop) ? shop.Layout.Find(p => PetShopCatalog.HouseCapacity(p.Id) > 0) : null;
        public int PetCapacity(OwnedShop shop) => Math.Min(PetShopCatalog.HouseCapacity(PetHouse(shop)?.Id), shop == null ? 0 : PetShopCatalog.MaximumCapacity(shop.Place.Size));
        private bool PetsStaying(OwnedShop shop) => PetGuests(shop).Count > 0;

        public bool PetOwnerHere(OwnedShop shop, PetStay stay)
        {
            var person = CityPersonState(stay?.OwnerId);
            return shop != null && person != null && person.Route == null && person.LocationPlaceId == shop.Place.Id
                && person.Task == CityPersonTask.Shopping && person.TaskLabel == PetOwnerVisitLabel;
        }

        private void PreparePetVisits(long now)
        {
            _petOwnerVisits.Clear();
            foreach (var shop in _shops)
            {
                if (!shop.Rented || !IsPetShop(shop)) continue;
                int capacity = PetCapacity(shop);
                if (capacity == 0) continue;
                if (!_petBoarding.TryGetValue(shop.Place.Id, out var state))
                {
                    state = new PetBoardingState { ShopId = shop.Place.Id, NextOfferAt = now + 25 };
                    _petBoarding.Add(shop.Place.Id, state);
                }
                if (state.Offer != null && now >= state.Offer.RequestedAt + 270) state.Offer = null;
                foreach (var stay in state.Stays) AgePetCondition(stay);
                bool open = shop.Sim.IsOpenAt(Clock.Now) && !IsGameOver;
                if (!open) continue;
                foreach (var stay in state.Stays)
                    if (now >= stay.NextVisitAt) _petOwnerVisits[stay.OwnerId] = shop;
                if (state.Stays.Length >= capacity || PetShopPendingMove(shop)) continue;
                if (state.Offer == null && now >= state.NextOfferAt)
                {
                    state.NextOfferAt = now + 45;
                    var owner = NextPetOwner(shop, now);
                    if (owner != null)
                    {
                        int seed = (int)((now + ConversationSeed(owner.PersonId)) % 1000 + 1000) % 1000;
                        var name = Enumerable.Range(0, PetNames.Length).Select(i => PetNames[(seed + i) % PetNames.Length])
                            .FirstOrDefault(n => !state.Stays.Any(p => p.Name == n)) ?? PetNames[seed % PetNames.Length];
                        bool dog = seed % 2 != 0;
                        state.Offer = new PetStay
                        {
                            Id = now, RequestedAt = now, OwnerId = owner.PersonId, Name = name, Dog = dog,
                            Weeks = 1 + seed % 3, Coat = seed % 4,
                        };
                        state.NextOfferAt = now + 95;
                    }
                }
                if (state.Offer != null) _petOwnerVisits[state.Offer.OwnerId] = shop;
            }
        }

        private CityPersonState NextPetOwner(OwnedShop shop, long now)
        {
            var reserved = new HashSet<string>(_petBoarding.Values.SelectMany(state => state.Stays).Select(p => p.OwnerId));
            foreach (var state in _petBoarding.Values)
                if (state.Offer != null) reserved.Add(state.Offer.OwnerId);
            var nearby = _cityPeople.Values.Where(p => p.Route == null && p.ActivityId == null && p.VisitId == null
                    && City.HasPlace(p.HomePlaceId) && City.GetPlace(p.HomePlaceId).Tier == shop.Place.Tier
                    && CityPerson(p.PersonId)?.Recruitable == false && !AuthoredCityWorkAt(p, now) && !reserved.Contains(p.PersonId))
                .OrderBy(p => DistanceCity(new MapPoint(p.X, p.Y), StandOf(shop.Place)))
                .ThenBy(p => p.PersonId, StringComparer.Ordinal).Take(8);
            return nearby.OrderByDescending(p => Random.Next(0,100) + Math.Min(30,Relation(p.PersonId))).FirstOrDefault();
        }

        private bool TickPetOwnerVisit(CityPersonState person, long now, bool onShift)
        {
            if (!_petOwnerVisits.TryGetValue(person.PersonId, out var shop)) return false;
            // If business hours and work shifts never overlap, the owner makes time for an overdue visit.
            if (onShift && !PetGuests(shop).Any(p => p.OwnerId == person.PersonId && now >= p.NextVisitAt + GameTimestamp.MinutesPerDay))
                return false;
            if (person.LocationPlaceId == shop.Place.Id)
            {
                person.Task = CityPersonTask.Shopping;
                person.TaskLabel = PetOwnerVisitLabel;
                return true;
            }
            var route = PlanCityRoute(person, shop.Place.Id, now);
            if (route == null) return false;
            person.Route = route;
            person.LocationPlaceId = null;
            person.Task = CityPersonTask.Travelling;
            person.TaskLabel = "前往" + ShopTitle(shop) + "办理宠物代养";
            return true;
        }

        public string PetAcceptBlocker(OwnedShop shop)
        {
            var state = PetBoarding(shop);
            if (state?.Offer == null) return "暂无送养订单";
            if (state.Stays.Length >= PetCapacity(shop)) return "宠物屋已经满员";
            if (PetShopPendingMove(shop)) return "这间店已经安排搬迁";
            if (!shop.Sim.IsOpen) return "营业时可以办理送养";
            return PetOwnerHere(shop, state.Offer) ? null : "主人正在来店";
        }

        private bool PetShopPendingMove(OwnedShop shop) => PendingLease != null && !PendingIsNewShop && (shop == MainShop || shop == _focus);

        public bool AcceptPetStay()
        {
            if (!InPlayerShop || !IsPetShop(_focus) || IsGameOver) return false;
            string reason = PetAcceptBlocker(_focus);
            if (reason != null) return Fail(reason);
            ReceivePet(_focus, PetBoarding(_focus));
            return true;
        }

        private void ReceivePet(OwnedShop shop, PetBoardingState state)
        {
            var stay = state.Offer;
            stay.NextVisitAt = Clock.Now.AddMinutes(WeekMinutes).TotalMinutes;
            stay.LastConditionDay = Clock.Today.TotalDays;
            foreach (var action in PetCareActions)
                if (SharedCareDay(state,action)==Clock.Today.TotalDays && UsesSharedCare(stay,action))
                    ApplySharedCareBenefit(state,stay,action);
            state.Stays = state.Stays.Append(stay).ToArray();
            state.Offer = null;
            Say(ShopTitle(shop) + " · " + stay.Name + "入住 " + stay.Weeks + " 周，主人每周来探望结算");
        }

        public string PetVisitBlocker(OwnedShop shop, PetStay stay)
        {
            if (stay == null || Clock.Now.TotalMinutes < stay.NextVisitAt) return "尚未到探望结算时间";
            if (PetOwnerHere(shop, stay)) return null;
            return "主人正在安排到店";
        }

        public static Money PetWeeklyPayment(PetStay stay) =>
            Money.FromYuan((long)Math.Round(stay.WeeklyPrice * (.45 + .85 * Math.Max(0, Math.Min(100,stay.Condition)) / 100.0)));

        public bool SettlePetStay(long id)
        {
            if (!InPlayerShop || !IsPetShop(_focus) || IsGameOver) return false;
            var state = PetBoarding(_focus);
            var stay = state?.Stays.FirstOrDefault(p => p.Id == id);
            string reason = PetVisitBlocker(_focus, stay);
            if (reason != null) return Fail(reason);
            SettlePet(_focus, state, stay);
            return true;
        }

        private void SettlePet(OwnedShop shop, PetBoardingState state, PetStay stay)
        {
            var amount = PetWeeklyPayment(stay);
            bool satisfied = stay.Condition >= 80;
            stay.PaidWeeks++;
            bool finished = stay.PaidWeeks >= stay.Weeks;
            if (finished) state.Stays = state.Stays.Where(p => p != stay).ToArray();
            else stay.NextVisitAt = Clock.Now.AddMinutes(WeekMinutes).TotalMinutes;
            Ledger.Credit(Clock.Today, "宠物代养", amount, stay.Name + " · 第 " + stay.PaidWeeks + " 周",
                LedgerAttribution.ForShop(shop.Place.Id, "pet:" + shop.Place.Id + ":" + stay.Id + ":" + stay.PaidWeeks, "代养服务"));
            if (satisfied) AddRelation(stay.OwnerId, 2);
            if (satisfied) shop.Sim.AdjustShopReputation(.6);
            else if (stay.Condition < 50) shop.Sim.AdjustShopReputation(-.6);
            state.LastPickup = stay.Name + (finished ? " · 主人已接回" : " · 主人已探望，继续代养") + " · 本周实收 " + amount
                + " · 状态 " + stay.Condition + "/100"
                + (satisfied ? " · 主人满意，交情 +2" : "");
            Say(ShopTitle(shop) + " · " + state.LastPickup);
        }

        private void TickPetShopStaff()
        {
            if (IsGameOver) return;
            foreach (var shop in _shops)
            {
                var state = PetBoarding(shop);
                if (state == null || !shop.Rented) continue;
                if (state.Stays.Length > 0 && Clock.Now.TotalMinutes >= state.NextStaffCheckAt)
                {
                    state.NextStaffCheckAt = Clock.Now.TotalMinutes + 15;
                    ApplyPetKeeperCare(shop,state);
                }
                if (CityPlayerCanWork(shop) || !shop.Sim.IsOpenAt(Clock.Now)) continue;
                if (state.Offer == null && !state.Stays.Any(p => Clock.Now.TotalMinutes >= p.NextVisitAt)) continue;
                if (!ShopStaff(shop, EmployeeRole.Cashier).Any(e => CityEmployeeAvailable(e, shop))) continue;
                foreach (var stay in state.Stays)
                    if (PetVisitBlocker(shop, stay) == null) SettlePet(shop, state, stay);
                if (PetAcceptBlocker(shop) == null) ReceivePet(shop, state);
            }
        }

        private static bool UsesSharedCare(PetStay pet,PetCareAction action) => action switch
        {
            PetCareAction.Water => true,
            PetCareAction.Litter => !pet.Dog,
            PetCareAction.CatFood => !pet.Dog,
            PetCareAction.DogFood => pet.Dog,
            _ => false
        };

        public bool HasPetsForCare(OwnedShop shop,PetCareAction action) => PetGuests(shop).Any(p=>UsesSharedCare(p,action));

        public bool PetCareDoneToday(OwnedShop shop,PetCareAction action) =>
            PetBoarding(shop) is PetBoardingState state && SharedCareDay(state,action)==Clock.Today.TotalDays;

        private void AgePetCondition(PetStay pet)
        {
            int day = Clock.Today.TotalDays;
            if (pet.LastConditionDay == day) return;
            if (pet.LastConditionDay >= 0)
            {
                int elapsed = Math.Min(100,Math.Max(0,day-pet.LastConditionDay));
                AdjustPetCondition(pet,-elapsed*(9+(100-pet.Happiness)/20));
                pet.Happiness = Math.Max(0,pet.Happiness-elapsed*10);
            }
            pet.LastConditionDay = day;
        }

        private static void AdjustPetCondition(PetStay pet,int delta) => pet.Condition = Math.Max(0,Math.Min(100,pet.Condition+delta));
        public static int PetKeeperCareBonus(Employee keeper) => 3 + EmployeeCharm(keeper) / 10;
        private Employee[] PetKeepers(OwnedShop shop) => ShopStaff(shop,EmployeeRole.PetKeeper).OrderBy(e => e.Id,StringComparer.Ordinal).ToArray();

        public (int coveredPets, int missingKeepers) PetKeeperCoverage(OwnedShop shop)
        {
            int petCount = PetGuests(shop).Count;
            if (petCount == 0) return (0, 0);

            var keepers = PetKeepers(shop);
            int required = (petCount + PetsPerKeeper - 1) / PetsPerKeeper;
            int assigned = Math.Min(keepers.Length, required);
            int covered = 0;
            // Keep the same pet groups as daily care even when a keeper is off duty.
            for (int i = 0; i < assigned; i++)
                if (CityEmployeeAvailable(keepers[i], shop))
                    covered += Math.Min(PetsPerKeeper, petCount - i * PetsPerKeeper);

            return (covered, required - assigned);
        }

        public Employee PetKeeperOf(OwnedShop shop,PetStay pet)
        {
            var state = PetBoarding(shop);
            if (state == null) return null;
            int index = Array.IndexOf(state.Stays,pet);
            if (index < 0) return null;
            var keepers = PetKeepers(shop);
            return index/PetsPerKeeper < keepers.Length ? keepers[index/PetsPerKeeper] : null;
        }

        private void ApplyPetKeeperCare(OwnedShop shop,PetBoardingState state)
        {
            var keepers = PetKeepers(shop);
            int day = Clock.Today.TotalDays;
            for (int i=0; i<keepers.Length && i*PetsPerKeeper<state.Stays.Length; i++)
            {
                var keeper = keepers[i];
                if (!CityEmployeeAvailable(keeper,shop)) continue;
                for (int j=i*PetsPerKeeper; j<Math.Min((i+1)*PetsPerKeeper,state.Stays.Length); j++)
                {
                    var pet = state.Stays[j];
                    if (pet.LastKeeperDay != day)
                    {
                        pet.LastKeeperDay = day;
                        AdjustPetCondition(pet,PetKeeperCareBonus(keeper));
                    }
                    foreach (var action in PetCareActions)
                        if (UsesSharedCare(pet,action)) TryCompleteSharedCare(shop,state,action);
                    var toy = PetToy(shop, pet.Dog);
                    RaisePetHappiness(pet, toy == null ? 12 : 18, PetShopCatalog.ToyHappinessLimit(toy?.Id));
                }
            }
        }

        public static readonly IReadOnlyList<PetCareAction> PetCareActions =
            Array.AsReadOnly(new[]{PetCareAction.Water,PetCareAction.Litter,PetCareAction.CatFood,PetCareAction.DogFood});

        public static string PetCareName(PetCareAction action) => action switch
        {
            PetCareAction.Water => "换水",
            PetCareAction.Litter => "换猫砂",
            PetCareAction.CatFood => "加猫粮",
            PetCareAction.DogFood => "加狗粮",
            _ => ""
        };

        private static IEnumerable<GiftDef> PetCareProducts(PetCareAction action) => PetShopCatalog.Products.Where(g=>
            action == PetCareAction.Litter ? PetShopCatalog.IsLitter(g.Id)
            : PetShopCatalog.IsFood(g.Id) && PetShopCatalog.IsDogFood(g.Id) == (action == PetCareAction.DogFood));

        public int PetCareStock(OwnedShop shop,PetCareAction action) =>
            action==PetCareAction.Water ? 0 : PetCareProducts(action).Sum(g=>shop.Sim.Stock.GoodsCount(g.Id));

        public bool CareForBoardedPets(PetCareAction action)
        {
            if (!InPlayerShop || PetCapacity(_focus) == 0 || IsGameOver) return false;
            if (!HasPetsForCare(_focus,action) || PetCareDoneToday(_focus,action)) return false;
            var state = PetBoarding(_focus);
            if (!TryCompleteSharedCare(_focus,state,action)) return Fail(PetCareName(action)+"失败，门店库存不足。");
            Say("宠物屋 · "+PetCareName(action)+"已完成。");
            return true;
        }

        private static ref int SharedCareDay(PetBoardingState state,PetCareAction action)
        {
            if (action == PetCareAction.Water) return ref state.WaterDay;
            if (action == PetCareAction.Litter) return ref state.LitterDay;
            if (action == PetCareAction.DogFood) return ref state.DogFoodDay;
            return ref state.CatFoodDay;
        }

        private bool TryCompleteSharedCare(OwnedShop shop,PetBoardingState state,PetCareAction action)
        {
            int today = Clock.Today.TotalDays;
            ref int day = ref SharedCareDay(state,action);
            if (day == today) return false;
            if (action != PetCareAction.Water)
            {
                var supply = PetCareProducts(action).FirstOrDefault(g=>shop.Sim.Stock.GoodsCount(g.Id)>0);
                if (supply == null || !shop.Sim.Stock.TryRemoveGoods(supply.Id,1)) return false;
                if (action == PetCareAction.CatFood) state.CatFoodTier = PetShopCatalog.Tier(supply.Id);
                if (action == PetCareAction.DogFood) state.DogFoodTier = PetShopCatalog.Tier(supply.Id);
            }
            day = today;
            foreach (var pet in state.Stays)
                if (UsesSharedCare(pet,action)) ApplySharedCareBenefit(state,pet,action);
            return true;
        }

        private static void ApplySharedCareBenefit(PetBoardingState state,PetStay pet,PetCareAction action) =>
            AdjustPetCondition(pet,action switch
            {
                PetCareAction.Water => pet.Dog ? 5 : 3,
                PetCareAction.Litter => 3,
                PetCareAction.CatFood => 6+state.CatFoodTier*3,
                PetCareAction.DogFood => 6+state.DogFoodTier*3,
                _ => 0
            });

        public bool PlayWithBoardedPet(long id, string toy = null)
        {
            if (!InPlayerShop || PetCapacity(_focus) == 0 || IsGameOver) return false;
            var pet = PetGuests(_focus).FirstOrDefault(p => p.Id == id);
            if (pet == null || toy != null && !PetToys(_focus, pet.Dog).Any(p => p.Id == toy)) return false;
            RaisePetHappiness(pet, toy == null ? 12 : 22, PetShopCatalog.ToyHappinessLimit(toy));
            return true;
        }

        private void RaisePetHappiness(PetStay pet, int bonus, int limit)
        {
            int day = Clock.Today.TotalDays;
            int previous = pet.LastPlayDay == day ? pet.LastPlayBonus : 0;
            int gain = Math.Max(0, Math.Min(bonus - previous, limit - pet.Happiness));
            pet.LastPlayDay = day;
            pet.LastPlayBonus = previous + gain;
            pet.Happiness += gain;
        }

        public PlacedFurniture PetToy(OwnedShop shop, bool dog) =>
            PetToys(shop, dog).OrderByDescending(p => PetShopCatalog.ToyTier(p.Id)).FirstOrDefault();

        public IEnumerable<PlacedFurniture> PetToys(OwnedShop shop, bool dog)
        {
            var house = PetHouse(shop);
            if (house == null) yield break;
            var rect = RoomPlanner.Footprint(RoomSpec.ForShop(shop.Place.Size), FurnitureCatalog.Get(house.Id), house);
            foreach (var item in shop.Layout)
                if (PetShopCatalog.IsToyFixture(item.Id) && PetShopCatalog.IsDogToy(item.Id) == dog && PetToyInReach(item, rect))
                    yield return item;
        }

        private static bool PetToyInReach(PlacedFurniture toy,RectD rect) =>
            toy.X >= rect.MinX - 1.4 && toy.X <= rect.MaxX + 1.4 && toy.Z >= rect.MinZ - 1.4 && toy.Z <= rect.MaxZ + 1.4;

        public bool BuyPetHouse(string id)
        {
            int capacity = PetShopCatalog.HouseCapacity(id);
            if (!InPlayerShop || !IsPetShop(_focus) || IsGameOver || capacity == 0) return false;
            if (capacity > PetShopCatalog.MaximumCapacity(_focus.Place.Size) || capacity <= PetCapacity(_focus))
                return Fail("这间铺子不能升级到该规格。");
            if (FurnitureToPlace(true, id) > 0) return Fail("已有同规格宠物屋待摆放。");
            var def = FurnitureCatalog.Get(id);
            if (!Ledger.TryDebit(Clock.Today, "装修", def.Price, def.Name, LedgerAttribution.ForShop(_focus.Place.Id)))
                return Fail("现金不足。");
            AddFurniture(id, 1);
            return true;
        }

        public bool BuyPetToy(string fixture)
        {
            if (!InPlayerShop || !IsPetShop(_focus) || !PetShopCatalog.IsToyFixture(fixture) || IsGameOver) return false;
            var def = FurnitureCatalog.Get(fixture);
            if (!Ledger.TryDebit(Clock.Today, "装修", def.Price, def.Name, LedgerAttribution.ForShop(_focus.Place.Id)))
                return Fail("现金不足。");
            AddFurniture(fixture, 1);
            return true;
        }

        private void SavePetBoarding(SaveData data)
        {
            data.petCareVersion = 2;
            data.petBoarding = _petBoarding.Values.Where(s => IsPetShop(ShopAt(s.ShopId))).Select(s => new PetBoardingState
            {
                ShopId = s.ShopId, NextOfferAt = s.NextOfferAt, LastPickup = s.LastPickup,
                NextStaffCheckAt = s.NextStaffCheckAt, WaterDay = s.WaterDay, LitterDay = s.LitterDay,
                CatFoodDay = s.CatFoodDay, DogFoodDay = s.DogFoodDay,
                CatFoodTier = s.CatFoodTier, DogFoodTier = s.DogFoodTier,
                Offer = Copy(s.Offer), Stays = s.Stays.Select(Copy).ToArray(),
            }).ToArray();
            PetStay Copy(PetStay pet) => pet == null ? null : new PetStay
            {
                Id = pet.Id, RequestedAt = pet.RequestedAt, NextVisitAt = pet.NextVisitAt, OwnerId = pet.OwnerId, Name = pet.Name,
                Dog = pet.Dog, Coat = pet.Coat, Weeks = pet.Weeks, PaidWeeks = pet.PaidWeeks,
                Condition = pet.Condition, LastConditionDay = pet.LastConditionDay, LastKeeperDay = pet.LastKeeperDay,
                LastPlayDay = pet.LastPlayDay, Happiness = pet.Happiness, LastPlayBonus = pet.LastPlayBonus,
            };
        }

        private void LoadPetBoarding(SaveData data)
        {
            _petBoarding.Clear();
            foreach (var state in data.petBoarding ?? Array.Empty<PetBoardingState>())
            {
                if (state?.ShopId == null || !IsPetShop(ShopAt(state.ShopId))) continue;
                state.Stays = state.Stays ?? Array.Empty<PetStay>();
                if (state.Offer != null && state.Offer.Weeks <= 0) state.Offer = null;
                if (data.petCareVersion == 0)
                    foreach (var pet in state.Stays.Append(state.Offer).Where(p=>p!=null))
                    {
                        pet.LastWaterDay = pet.LastLitterDay = -1;
                        pet.Happiness = 55;
                    }
                foreach (var pet in state.Stays.Where(p => p.Weeks <= 0))
                {
                    pet.Weeks = 1;
                    pet.Condition = 68;
                    pet.LastConditionDay = Clock.Today.TotalDays;
                    pet.NextVisitAt = Clock.Now.AddMinutes(WeekMinutes).TotalMinutes;
                }
                if (data.petCareVersion < 2) MigrateSharedCare(state);
                _petBoarding[state.ShopId] = state;
            }
        }

        private void MigrateSharedCare(PetBoardingState state)
        {
            int today = Clock.Today.TotalDays;
            foreach (var action in PetCareActions)
            {
                ref int day = ref SharedCareDay(state,action);
                var pets = state.Stays.Where(p=>UsesSharedCare(p,action)).ToArray();
                day = pets.Any(p=>OldCareDay(p,action)==today) ? today : -1;
                if (day == today)
                    foreach (var pet in pets)
                        if (OldCareDay(pet,action)!=today) ApplySharedCareBenefit(state,pet,action);
            }

            int OldCareDay(PetStay pet,PetCareAction action) => action==PetCareAction.Water ? pet.LastWaterDay
                : action==PetCareAction.Litter ? pet.LastLitterDay : pet.LastFedDay;
        }
    }
}
