using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Home;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Shop;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    public enum OpeningStaffLesson { None, StaffTab, Roles, PostRecruitment, Hire, Purchase }
    public enum OpeningRenovationLesson { None, Travel, RenovationTab, StartPlacement }

    public sealed partial class GameSession
    {
        private const string OpeningStockMigrationFlag = "tutorial:opening-stock-purchase-checked";
        private const string SecondhandShelfMigrationFlag = "tutorial:opening-secondhand-shelf-checked";
        private const string RestaurantShelfMigrationFlag = "tutorial:opening-restaurant-shelf-checked";

        // The rent reward marker is set by the prebuilt-shop setup, which already supplies stock.
        internal Place OpeningSupplyMarket => HasShop && OpeningRewardKind("rent") == null
            && ShopSupplyMarketId(RetailKind) is string id ? PurchaseDestination(City.GetPlace(id)) : null;

        internal bool HasOpeningStockPurchase(Place market)
        {
            int signedDay = CurrentShop.Lease?.SignedDay ?? 0;
            string wholesaleMemo = market.Name + "批发 ";
            string partsMemo = market.Name + " ";
            return Ledger.Entries.Any(entry =>
            {
                if (entry.Date.TotalDays < signedDay || !entry.Amount.IsNegative) return false;
                if (RetailKind == "restaurant" && entry.Category == "买菜") return true;
                if (entry.Category != "进货") return false;
                if (RetailKind == "computer")
                    return entry.Memo?.StartsWith(partsMemo, StringComparison.Ordinal) == true;
                if (entry.Memo?.StartsWith(wholesaleMemo, StringComparison.Ordinal) != true) return false;
                string product = entry.Attribution?.ProductId;
                return RetailKind == "restaurant" ? product?.StartsWith("produce:", StringComparison.Ordinal) == true
                    : RetailShopTypes.Sells(RetailKind, product);
            });
        }

        private void MigrateOpeningStockProgress()
        {
            // Older saves completed stock from the free furniture-store reward.
            if (Flags.Contains(OpeningStockMigrationFlag) || QuestCompleted("sale")
                || !(OpeningSupplyMarket is Place market)) return;
            Flags.Add(OpeningStockMigrationFlag);
            if (QuestCompleted("stock") && !HasOpeningStockPurchase(market)) Flags.Remove("tutorial:stock");
        }

        private void MigrateSecondhandShelfProgress()
        {
            if (!HasShop || RetailKind != "secondhand" || QuestCompleted("sale") || !Flags.Add(SecondhandShelfMigrationFlag)) return;
            // Older saves marked this step complete when the shelf was merely placed.
            if (ShelvesOfKind("secondhand") == 0) Flags.Remove("tutorial:shelf");
        }

        private void MigrateRestaurantShelfProgress()
        {
            if (!HasShop || CurrentShop != MainShop || RetailKind != "restaurant" || QuestCompleted("sale")
                || !Flags.Add(RestaurantShelfMigrationFlag)) return;
            if (!OpeningFurniturePlaced(CurrentShop, FurnitureCatalog.Shelf))
            {
                Flags.Remove("tutorial:place");
                if (MissingOpeningFurniture().Contains(FurnitureCatalog.Shelf)) Flags.Remove("tutorial:mall");
            }
            if (!HasRestaurantDrinkShelf(CurrentShop)) Flags.Remove("tutorial:shelf");
        }

        private const string OpeningChoicePrefix="shop:choose-kind:";
        public string PendingShopKindPlaceId=>Flags.Where(f=>f.StartsWith(OpeningChoicePrefix))
            .Select(f=>f.Substring(OpeningChoicePrefix.Length)).FirstOrDefault(id=>ShopAt(id)!=null||PendingLease?.PlaceId==id);
        public bool SignLeaseThenChooseKind(string placeId,bool newShop)
        {
            if(PendingShopKindPlaceId!=null)return Fail("请先确认上一间铺子的经营类型。");
            if(!City.HasPlace(placeId))return false;
            bool choose=(!HasShop||newShop)&&!IsDedicatedVenue(City.GetPlace(placeId));
            if(choose)Flags.Add(OpeningChoicePrefix+placeId);
            if(SignLease(placeId,newShop))return true;
            if(choose)Flags.Remove(OpeningChoicePrefix+placeId);
            return false;
        }
        public bool ConfirmOpeningKind(string placeId,string kind)
        {
            if(PendingShopKindPlaceId!=placeId)return Fail("这间店已完成经营类型确认。");
            string blocker=RetailTypeBlocker(kind);if(blocker!=null)return Fail(blocker);
            Flags.Remove(OpeningChoicePrefix+placeId);Flags.Remove("shop:choose-first:"+placeId);
            if(PendingLease?.PlaceId==placeId)PendingRetailKind=kind;
            else
            {
                var previous=_focus;
                SetFocus(ShopAt(placeId));SetOpeningShopKind(kind);RefreshQuestProgress();
                SetFocus(previous);
            }
            Say("已确认经营"+RetailShopTypes.Name(kind)+(PendingLease?.PlaceId==placeId?"，交房后发放尚未领取的开店任务奖励。":"。"));
            return true;
        }
        private void SetOpeningShopKind(string kind)
        {
            if(IsDedicatedVenue(_focus.Place))return;
            _focus.RetailKind=RetailShopTypes.Valid(kind);_focus.Sim.RetailKind=_focus.RetailKind;
            if(!Flags.Contains(OpeningChoicePrefix+_focus.Place.Id))Flags.Add("shop:main-category:"+_focus.Place.Id);
            UpdateShopFit();
        }

        private static readonly string[] RestaurantServiceFurniture =
        {
            FurnitureCatalog.ShopCounter, "restaurant_stove",
            "restaurant_table", FurnitureCatalog.FridgeSmall
        };

        public static string[] OpeningFurniture(string kind)=>kind=="restaurant"
            ?RestaurantServiceFurniture.Append(FurnitureCatalog.Shelf).ToArray()
            :kind=="secondhand"?new[]{"used_buydesk","used_display",FurnitureCatalog.Shelf,FurnitureCatalog.ShopCounter}
            :kind==PetShopCatalog.ShopKind?new[]{FurnitureCatalog.Shelf,FurnitureCatalog.ShopCounter,PetShopCatalog.SmallHouse}
            :new[]{FurnitureCatalog.Shelf,FurnitureCatalog.ShopCounter};

        public static bool MatchesOpeningFurniture(string id, FurnitureDef item) => item != null && (item.Id == id
            || id == FurnitureCatalog.Shelf && item.Fixture == FixtureKind.Shelf
            || id == FurnitureCatalog.FridgeSmall && item.Role == FurnitureRole.Fridge
            || id == PetShopCatalog.SmallHouse && PetShopCatalog.HouseCapacity(item.Id) > 0);

        public static bool OpeningFurniturePlaced(OwnedShop shop, string id)
        {
            foreach (var placed in shop.Layout)
                if (MatchesOpeningFurniture(id, FurnitureCatalog.Get(placed.Id))) return true;
            return false;
        }

        public static string OpeningFurnitureName(string id) => id == FurnitureCatalog.FridgeSmall ? "冰箱" : FurnitureCatalog.Get(id).Name;

        internal static bool HasRestaurantDrinkShelf(OwnedShop shop) => shop.Layout.Any(item =>
            item.Item == "drink" && FurnitureCatalog.Get(item.Id)?.Fixture == FixtureKind.Shelf);
        internal const string RestaurantDrinkShelfHint =
            "走到货架旁按 F，或在「库存」选择货架。点「选择摆什么」或「换品类」，从「零售商品」里选「饮料」。堂食顾客可以加购陈列的饮料。";

        private const string OpeningMallTravelSeen = "tutorial:opening-mall-travel";
        private bool _openingMallTutorialFocused;
        public Place OpeningFurnitureMall => City.Places.FirstOrDefault(place => place.Business == MallBusiness);

        public bool OpeningMallTravelPending => !QuestCompleted("mall") && !QuestCompleted("sale")
            && !Flags.Contains(OpeningMallTravelSeen) && CanPauseForTutorial && !InLayoutHistoryPlace
            && CityIntent == null && !AtMall && LeaveBlocker == null && ExpansionStorefront(1) != null
            && QuestStageReady(Quests.Get("mall")) && OpeningFurnitureMall is Place mall
            && (Vehicle != VehicleKind.Taxi || CityDestinationTravelBlocker(mall, out _, out _) == null);

        private bool OpeningMallTutorialPaused => _openingMallTutorialFocused && OpeningMallTravelPending;

        public void FocusOpeningMallTutorial(bool focused) => _openingMallTutorialFocused = focused;

        public void StartOpeningMallTravel()
        {
            if (!OpeningMallTravelPending) return;
            var mall = OpeningFurnitureMall;
            if (BeginCityTravel(mall.Id) && (CityIntent?.TargetPlaceId == mall.Id || AtMall))
                Flags.Add(OpeningMallTravelSeen);
        }

        private const string OpeningRenovationTravelSeen = "tutorial:opening-renovation-travel";
        private const string OpeningRenovationTabSeen = "tutorial:opening-renovation-tab";
        private const string OpeningRenovationStarted = "tutorial:opening-renovation-started";

        private bool OpeningRenovationReady => !QuestCompleted("place") && !QuestCompleted("sale")
            && !Flags.Contains(OpeningRenovationStarted) && CanPauseForTutorial && !InLayoutHistoryPlace
            && CityIntent == null && QuestStageReady(Quests.Get("place"));

        public OpeningRenovationLesson OpeningRenovationStep
        {
            get
            {
                if (!OpeningRenovationReady || ExpansionStorefront(1) is not OwnedShop shop)
                    return OpeningRenovationLesson.None;
                if (CurrentPlace != shop.Place)
                    return !Flags.Contains(OpeningRenovationTravelSeen) && LeaveBlocker == null
                        && ClosedReason(shop.Place) == null && MissingOpeningFurniture(shop).Length == 0
                        && (Vehicle != VehicleKind.Taxi || CityDestinationTravelBlocker(shop.Place, out _, out _) == null)
                        ? OpeningRenovationLesson.Travel : OpeningRenovationLesson.None;

                if (!Flags.Contains(OpeningRenovationTabSeen)) return OpeningRenovationLesson.RenovationTab;
                if (LifeQuietBlock(shop.Place.Id)) return OpeningRenovationLesson.None;
                var missing = OpeningFurniture(shop.RetailKind).Where(id => !OpeningFurniturePlaced(shop, id)).ToArray();
                return FurnitureToPlaceList(true).Any(item => item.Value > 0
                    && missing.Any(id => MatchesOpeningFurniture(id, FurnitureCatalog.Get(item.Key))))
                    ? OpeningRenovationLesson.StartPlacement : OpeningRenovationLesson.None;
            }
        }

        public void StartOpeningRenovationTravel()
        {
            if (OpeningRenovationStep == OpeningRenovationLesson.Travel && BeginCityTravel(ExpansionStorefront(1).Place.Id))
                Flags.Add(OpeningRenovationTravelSeen);
        }

        public void VisitOpeningRenovationTab()
        {
            if (OpeningRenovationStep != OpeningRenovationLesson.RenovationTab) return;
            Flags.Add(OpeningRenovationTravelSeen);
            Flags.Add(OpeningRenovationTabSeen);
        }

        private void RecordOpeningRenovationStarted()
        {
            if (InPlayerShop && CurrentShop == ExpansionStorefront(1) && QuestCompleted("mall") && !QuestCompleted("place"))
                Flags.Add(OpeningRenovationStarted);
        }

        private const string OpeningShelfStockSeen = "tutorial:opening-shelf-stock";

        public bool OpeningShelfTutorialPaused => !QuestCompleted("shelf") && !QuestCompleted("sale")
            && QuestCompleted("place") && CanPauseForTutorial && InPlayerShop && !InLayoutHistoryPlace
            && !IsDedicatedVenue(CurrentPlace) && CurrentShop == FirstStaffingShop
            && RetailShopTypes.All.Contains(RetailKind) && ShelvesPlaced > 0;

        public bool OpeningShelfStockVisited => Flags.Contains(OpeningShelfStockSeen);

        public void VisitOpeningShelfStock()
        {
            if (OpeningShelfTutorialPaused) Flags.Add(OpeningShelfStockSeen);
        }

        public int OpeningShelfTutorialIndex()
        {
            var shelves = ShelfIndexes();
            foreach (int index in shelves)
                if (ShelfKindAt(index) == null) return index;
            return shelves.Count == 0 ? -1 : shelves[0];
        }

        public ShelfKind OpeningShelfTutorialKind()
        {
            if (RetailKind == "restaurant") return ShelfKinds.Get("drink");
            if (RetailKind == "secondhand") return ShelfKinds.Get("secondhand");
            if (RetailKind == "computer")
                return ShelfKinds.PartKinds.FirstOrDefault(kind => PartCatalog.InCategory(kind.Category)
                    .Any(part => PlayerShop.Stock.CountAll(part) > 0)) ?? ShelfKinds.Get("cpu");

            var categories = ShelfKinds.GoodsKinds.Where(kind => kind.GiftIds.Any(id => RetailShopTypes.Sells(RetailKind, id)));
            return categories.FirstOrDefault(kind => kind.GiftIds.Any(id => RetailShopTypes.Sells(RetailKind, id)
                && PlayerShop.Stock.GoodsCount(id) > 0)) ?? categories.FirstOrDefault();
        }

        private const string OpeningCounterTabSeen = "tutorial:opening-counter-tab-confirmed";
        private const string OpeningPetIntakeSeen = "tutorial:opening-pet-intake-completed";
        private const string OpeningUsedDeskDone = "tutorial:opening-used-desk-done";
        private const string OpeningUsedPurchasePrefix = "tutorial:opening-used-purchase:";
        private bool _openingCounterTutorialFocused;

        private bool AtTutorialCounter => CanPauseForTutorial
            && InPlayerShop && !IsRenovating && !InLayoutHistoryPlace && CityIntent == null;
        private bool AtOpeningCounter => AtTutorialCounter && CurrentShop == ExpansionStorefront(1);

        public bool OpeningCounterLessonAvailable => !QuestCompleted("sale") && AtOpeningCounter && QuestStageReady(Quests.Get("sale"));
        public bool OpeningPetIntakeLessonAvailable
        {
            get
            {
                if (!AtTutorialCounter || Flags.Contains(OpeningPetIntakeSeen)) return false;
                var shop = CurrentShop;
                var boarding = PetBoarding(shop);
                return boarding?.Offer != null && boarding.Stays.Length == 0 && string.IsNullOrEmpty(boarding.LastPickup)
                    && PetAcceptBlocker(shop) == null;
            }
        }

        private bool OpeningUsedDeskLessonAvailable => RetailKind == "secondhand" && !QuestCompleted("hire")
            && !Flags.Contains(OpeningUsedDeskDone) && AtOpeningCounter && QuestStageReady(Quests.Get("stock"));

        public bool OpeningCounterTutorialPaused => _openingCounterTutorialFocused
            && (OpeningCounterLessonAvailable || OpeningUsedDeskLessonAvailable || OpeningPetIntakeLessonAvailable);
        public bool OpeningCounterTabVisited => Flags.Contains(OpeningCounterTabSeen);

        public void FocusOpeningCounterTutorial(bool focused) => _openingCounterTutorialFocused = focused;

        public void VisitOpeningCounterTab()
        {
            if (OpeningCounterTutorialPaused) Flags.Add(OpeningCounterTabSeen);
        }

        public UsedItem OpeningUsedDeskLessonItem()
        {
            var shop = CurrentShop;
            if (!OpeningUsedDeskLessonAvailable || !SpecialPlayer(shop)
                || !_specialBusinesses.TryGetValue(shop.Place.Id, out var state)) return null;

            string storePrefix = OpeningUsedPurchasePrefix + shop.Place.Id + ":";
            string purchase = Flags.FirstOrDefault(flag => flag.StartsWith(storePrefix, StringComparison.Ordinal));
            if (purchase != null && int.TryParse(purchase.Substring(storePrefix.Length), out int id))
            {
                var bought = state.Used.FirstOrDefault(item => item.Id == id && item.Owned);
                if (bought != null)
                    return !bought.Sold && !bought.Listed && bought.RepairWorkRemaining == 0
                        && (!bought.Inspected || UsedShelfListingBlocker(shop, bought) == null) ? bought : null;
            }
            if (!CanServeSpecialShop(shop, Clock.Now, true) || UsedStockCount(state) >= UsedStockLimit(shop)) return null;
            long now = Clock.Now.TotalMinutes;
            return state.Used.FirstOrDefault(item => item.WaitingForPurchase && item.ExpiresAt > now
                && Ledger.CanAfford(Money.FromYuan(item.Asking)) && UsedShelfListingBlocker(shop, item) == null);
        }

        public void RecordOpeningUsedDeskPurchase(int id)
        {
            string prefix = OpeningUsedPurchasePrefix + CurrentShop.Place.Id + ":";
            Flags.RemoveWhere(flag => flag.StartsWith(prefix, StringComparison.Ordinal));
            Flags.Add(prefix + id);
        }

        public void CompleteOpeningUsedDeskLesson() => Flags.Add(OpeningUsedDeskDone);

        public (DiningOrder order, RestaurantJob job) OpeningRestaurantCounterAction()
        {
            if (RetailKind != "restaurant" || !_specialBusinesses.TryGetValue(CurrentShop.Place.Id, out var state)
                || state.Workers.Any(worker => worker.Busy && worker.ActorId == RestaurantPlayer)) return default;

            var orders = state.Orders.Where(order => !order.IsFishOrder && !order.TableCleared).ToArray();
            var available = orders.Where(order => !state.Workers.Any(worker => worker.Busy && worker.OrderId == order.Id));
            long now = Clock.Now.TotalMinutes;
            var payment = available.FirstOrDefault(order => order.Stage == DiningStage.Checkout);
            if (payment != null && Furnished(CurrentShop, FurnitureCatalog.ShopCounter) > 0)
                return (payment, RestaurantJob.Clear);

            var burnt = available.FirstOrDefault(order => order.Stage == DiningStage.Burnt);
            if (!CanServeSpecialShop(CurrentShop, Clock.Now, true))
                return burnt == null ? default : (burnt, RestaurantJob.Clear);
            var ready = available.FirstOrDefault(order => JobMatches(RestaurantJob.Serve, order, now));
            if (ready != null) return (ready, RestaurantJob.Serve);
            if (burnt != null) return (burnt, RestaurantJob.Clear);
            // Let the first dish finish before asking the player to start the next table.
            if (orders.Any(order => order.Stage is DiningStage.Cooking or DiningStage.Ready or DiningStage.Eating or DiningStage.Checkout)
                || state.Workers.Any(worker => worker.Busy && worker.Job == RestaurantJob.Cook))
                return default;
            int stoves = Furnished(CurrentShop, "restaurant_stove");
            int occupied = state.Orders.Where(order => order.Stage is DiningStage.Cooking or DiningStage.Burnt)
                .Select(order => order.Stove).Where(stove => stove >= 0 && stove < stoves).Distinct().Count();
            if (occupied >= stoves) return default;
            var waiting = available.FirstOrDefault(order => order.Stage == DiningStage.Waiting
                && RestaurantIngredientBlocker(CurrentShop, order) == null);
            return waiting == null ? default : (waiting, RestaurantJob.Cook);
        }

        private string OpeningRewardKind(string id)=>Flags.FirstOrDefault(f=>f.StartsWith("tutorial:reward:"+id+":"))?.Substring(("tutorial:reward:"+id+":").Length);
        public string[] MissingOpeningFurniture() => MissingOpeningFurniture(CurrentShop);

        private string[] MissingOpeningFurniture(OwnedShop shop)
        {
            if (shop?.Rented != true) return Array.Empty<string>();
            return OpeningFurniture(shop.RetailKind).Where(id => !OpeningFurniturePlaced(shop, id)
                && !FurnitureCatalog.All.Any(item => MatchesOpeningFurniture(id, item) && FurnitureOwnedUnplaced(item.Id) > 0)).ToArray();
        }

        private OwnedShop ExpansionStorefront(int number) => _shops
            .Where(shop => shop.Rented && !IsDedicatedVenue(shop.Place)).Skip(number - 1).FirstOrDefault();

        private static readonly IReadOnlyList<EmployeeRole> RetailOpeningRoles = Array.AsReadOnly(new[]
            { EmployeeRole.Cashier, EmployeeRole.Buyer });
        private static readonly IReadOnlyList<EmployeeRole> DiningOpeningRoles = Array.AsReadOnly(new[]
            { EmployeeRole.Waiter, EmployeeRole.Chef, EmployeeRole.Buyer });
        private static readonly IReadOnlyList<EmployeeRole> PetShopOpeningRoles = Array.AsReadOnly(new[]
            { EmployeeRole.Cashier, EmployeeRole.Buyer, EmployeeRole.PetKeeper });
        private static readonly IReadOnlyList<EmployeeRole> ResaleOpeningRoles = Array.AsReadOnly(new[]
            { EmployeeRole.Appraiser, EmployeeRole.Cashier });
        private static readonly IReadOnlyList<EmployeeRole> VenueOpeningRoles = Array.AsReadOnly(new[]
            { EmployeeRole.Cashier });

        internal OwnedShop FirstStaffingShop => ExpansionStorefront(1) ?? (HasShop ? CurrentShop : null);

        private const string OpeningSaleCompletionFlag = "tutorial:opening-sale-completion-pending";
        private const string OpeningSaleCompletionSeen = "tutorial:opening-sale-completion-seen";
        public bool OpeningSaleCompletionPending => !Flags.Contains(OpeningSaleCompletionSeen)
            && (Flags.Contains(OpeningSaleCompletionFlag)
                || (QuestCompleted("sale") && !QuestCompleted("hire") && !Flags.Contains(StaffTabSeen)));

        public void ConfirmOpeningSaleCompletion()
        {
            Flags.Remove(OpeningSaleCompletionFlag);
            Flags.Add(OpeningSaleCompletionSeen);
        }

        private const string StaffTabSeen = "tutorial:opening-staff-tab";
        private const string StaffRolesSeen = "tutorial:opening-staff-roles";
        private const string OpeningStaffRested = "tutorial:opening-staff-rested";
        private long _openingStaffWaitUntil;

        private bool FirstRecruitmentNotScheduled => _openingStaffWaitUntil == 0 && !EverHired
            && !QuestCompleted("hire") && MyShopCount == 1
            && FirstStaffingShop is OwnedShop shop && !IsDedicatedVenue(shop.Place);

        public OpeningStaffLesson OpeningStaffStep
        {
            get
            {
                if (QuestCompleted("hire") || OpeningSaleCompletionPending
                    || !CanPauseForTutorial || !InPlayerShop || CurrentShop != FirstStaffingShop
                    || IsDedicatedVenue(CurrentPlace) || IsRenovating
                    || !QuestStageReady(Quests.Get("hire"))) return OpeningStaffLesson.None;
                if (!Flags.Contains(StaffTabSeen)) return OpeningStaffLesson.StaffTab;
                if (!Flags.Contains(StaffRolesSeen)) return OpeningStaffLesson.Roles;
                if (FirstRecruitmentNotScheduled && CanGuideOpeningStaffWait(CurrentShop)) return OpeningStaffLesson.PostRecruitment;
                if (OpeningStaffWaitTarget(CurrentShop) != null) return OpeningStaffLesson.None;
                var missing = MissingOpeningStaffRoles(CurrentShop).ToArray();
                if (missing.Length > 0)
                    return Candidates.Any(candidate => missing.Any(role => RecruitAssignmentBlocker(candidate.Id, CurrentShop.Place.Id, role) == null))
                        ? OpeningStaffLesson.Hire : OpeningStaffLesson.None;
                return OpeningStaffRoles(CurrentShop).Contains(EmployeeRole.Buyer) && !OpeningBuyerPlanReady(CurrentShop)
                    ? OpeningStaffLesson.Purchase : OpeningStaffLesson.None;
            }
        }

        public void VisitOpeningStaffTab()
        {
            if (OpeningStaffStep == OpeningStaffLesson.StaffTab) Flags.Add(StaffTabSeen);
        }

        public bool FinishOpeningStaffRoles() => OpeningStaffStep == OpeningStaffLesson.Roles && Flags.Add(StaffRolesSeen);

        private bool CanGuideOpeningStaffWait(OwnedShop shop) => shop != null && shop == FirstStaffingShop
            && !QuestCompleted("hire") && !IsDedicatedVenue(shop.Place) && !Candidates.Any(CandidateAvailable)
            && NextOpeningStaffRole(shop) != null && !EmployeesForShop(shop).Any(row => row.Role == EmployeeRole.Unassigned && row.Shop == null);

        private void RecordOpeningStaffRest()
        {
            if (OpeningStaffWaitTarget(FirstStaffingShop) == QuestTarget.Bed)
                Flags.Add(OpeningStaffRested);
        }

        private QuestTarget? OpeningStaffWaitTarget(OwnedShop shop)
        {
            if (_openingStaffWaitUntil <= Clock.Now.TotalMinutes || !CanGuideOpeningStaffWait(shop)) return null;
            long arrivalDayStart = _openingStaffWaitUntil - CandidateMinute;
            bool overnightShift = shop.Sim.CloseMinute < shop.Sim.OpenMinute && Clock.MinuteOfDay < shop.Sim.CloseMinute;
            bool firstShift = Clock.Now.TotalMinutes < arrivalDayStart || overnightShift;
            if ((firstShift && shop.Sim.InHours(Clock.Now)) || ShopFinishingService(shop)) return QuestTarget.Counter;
            if (Flags.Contains(OpeningStaffRested) || _sleepHistory.Any(span => span[1] >= arrivalDayStart))
                return QuestTarget.Staff;
            return shop.Sim.AllDay ? QuestTarget.Counter : QuestTarget.Bed;
        }

        private string OpeningStaffCounterInstruction(OwnedShop shop)
        {
            if (!shop.Sim.InHours(Clock.Now)) return "招待完店内的顾客，打烊后再回家。";
            return shop.Sim.AllDay ? "继续在店里营业。" : $"继续营业到 {Hm(shop.Sim.CloseMinute)} 打烊。";
        }

        public IReadOnlyList<EmployeeRole> OpeningStaffRoles(OwnedShop shop) => IsDedicatedVenue(shop.Place)
            ? VenueOpeningRoles : shop.RetailKind switch
            {
                "restaurant" => DiningOpeningRoles,
                "secondhand" => ResaleOpeningRoles,
                PetShopCatalog.ShopKind => PetShopOpeningRoles,
                _ => RetailOpeningRoles
            };

        public string OpeningStaffRoleName(OwnedShop shop, EmployeeRole role) =>
            role == EmployeeRole.Cashier && IsDedicatedVenue(shop.Place) ? "场馆值守员" : RoleName(role);

        public string OpeningStaffRoleDuty(OwnedShop shop, EmployeeRole role) => role switch
        {
            EmployeeRole.Waiter => "上菜、收桌并收款",
            EmployeeRole.Chef => "按订单备料、下锅和出锅",
            EmployeeRole.Buyer => shop.RetailKind == "restaurant" ? "采购大米和配菜，给厨房补货" : "按清单或库存目标补货",
            EmployeeRole.Appraiser => "向卖家收购、鉴定并上架旧物",
            EmployeeRole.PetKeeper => "给寄养宠物喂食、换水、清理和陪玩",
            EmployeeRole.Repairer => "在修复台处理有瑕疵或损坏的旧物",
            _ => IsDedicatedVenue(shop.Place) ? "接待顾客并维持营业" : "为排队顾客结账"
        };

        public string OpeningStaffSpecialty(OwnedShop shop) => IsDedicatedVenue(shop.Place) ? null : shop.RetailKind switch
        {
            "computer" => "接整机订单时安排装机员，摆好装机台、展示桌，再开启自动装机。",
            "models" => "做改色委托时摆好喷漆台，安排喷漆师接手。",
            "clothing" => "需要导购时可加一名销售员，帮助提高成交率。",
            _ => null
        };

        public bool OpeningBuyerPlanReady(OwnedShop shop) => ShopStaff(shop, EmployeeRole.Buyer).Any(buyer =>
            buyer.PurchasePlan is { Daily: true } plan && (plan.InventoryThreshold
                ? HasEmployeeRestockTarget(shop, buyer.Id) : plan.Items?.Length > 0));

        public IEnumerable<EmployeeRole> MissingOpeningStaffRoles(OwnedShop shop) =>
            OpeningStaffRoles(shop).Where(role => !ShopStaff(shop, role).Any());

        public EmployeeRole? NextOpeningStaffRole(OwnedShop shop) =>
            MissingOpeningStaffRoles(shop).Select(role => (EmployeeRole?)role).FirstOrDefault();

        private static string OpeningStaffSelfFlag(OwnedShop shop) => $"tutorial:staff-self:{shop.Place.Id}:{shop.RetailKind}";

        internal bool OpeningStaffDone(OwnedShop shop) => shop != null && (Flags.Contains(OpeningStaffSelfFlag(shop))
            || (NextOpeningStaffRole(shop) == null
                && (!OpeningStaffRoles(shop).Contains(EmployeeRole.Buyer) || OpeningBuyerPlanReady(shop))));

        public string OpeningStaffNextStep(OwnedShop shop)
        {
            if (OpeningStaffWaitTarget(shop) is QuestTarget waiting)
                return waiting switch
                {
                    QuestTarget.Bed => $"回家休息，{NextCandidateArrivalText}再到店招聘。",
                    QuestTarget.Staff => CurrentPlace == shop.Place ? $"等应聘者 {NextCandidateArrivalText} 到店。"
                        : $"前往店铺，应聘者 {NextCandidateArrivalText} 到店。",
                    _ => OpeningStaffCounterInstruction(shop)
                };
            if (FirstRecruitmentNotScheduled && CanGuideOpeningStaffWait(shop))
            {
                if (!QuestCompleted("sale")) return "完成首单后可贴招聘告示。";
                return Flags.Contains(StaffRolesSeen) ? "在员工页点「贴招聘告示」，安排首批应聘者明天到店。"
                    : "点员工页「开店分工」旁的问号。";
            }
            if (NextOpeningStaffRole(shop) is EmployeeRole role)
            {
                string name = OpeningStaffRoleName(shop, role);
                if (StaffCapacityBlocker(shop) != null)
                    return $"在「管理岗位与排班」里调整员工分工，安排一名{name}。";
                if (Candidates.Any(CandidateAvailable))
                {
                    string choices = string.Join("、", MissingOpeningStaffRoles(shop).Select(missing => OpeningStaffRoleName(shop, missing)));
                    return choices == name ? $"选一位应聘者，聘用为{name}。" : $"选一位应聘者，安排{choices}中的一个岗位。";
                }
                if (EmployeesForShop(shop).Any(row => row.Role == EmployeeRole.Unassigned && row.Shop == null))
                    return $"在「管理岗位与排班」里把待安排员工设为{name}。";
                return RecruitmentNoticePosted ? $"应聘者 {NextCandidateArrivalText} 到店后，聘用{name}。"
                    : $"在员工页贴招聘告示，招聘{name}。";
            }
            return OpeningStaffRoles(shop).Contains(EmployeeRole.Buyer) && !OpeningBuyerPlanReady(shop)
                ? "点进货员旁的「安排进货」，保存采购计划。" : null;
        }

        public string OpeningStaffPurchaseHint(OwnedShop shop)
        {
            string goods = shop.RetailKind == "restaurant" ? "大米和配菜" : "本店经营的商品";
            return $"点进货员旁的「安排进货」，把{goods}加进「固定清单」。选频率和交通，核对计划费用，点「完成」保存。";
        }

        internal QuestDef OpeningStaffGuide(QuestDef source, OwnedShop shop)
        {
            var guide = source.Copy();
            var waiting = OpeningStaffWaitTarget(shop);
            guide.Target = waiting ?? QuestTarget.Staff;
            guide.GuideDestination = guide.Target == QuestTarget.Bed ? null : shop.Place;
            guide.Short = OpeningStaffNextStep(shop) ?? "查看员工分工";
            if (waiting.HasValue)
            {
                guide.Hint = waiting.Value switch
                {
                    QuestTarget.Counter => $"{guide.Short} 应聘者 {NextCandidateArrivalText} 到店。",
                    QuestTarget.Bed => $"回家休息，下一批应聘者 {NextCandidateArrivalText} 到店，18:00 离开。",
                    _ => $"可以先到店。应聘者 {NextCandidateArrivalText} 到店后，在「员工」页选择要聘用的人。"
                };
                return guide;
            }
            var roles = OpeningStaffRoles(shop);
            var duties = roles.Select(role => $"{OpeningStaffRoleName(shop, role)}：{OpeningStaffRoleDuty(shop, role)}。");
            guide.Hint = string.Join("\n", duties) + "\n" + RecruitmentHoursText
                + (roles.Contains(EmployeeRole.Buyer) ? "\n" + OpeningStaffPurchaseHint(shop) : "");
            guide.Progress = _ => $"岗位已安排 {roles.Count(role => ShopStaff(shop, role).Any())}/{roles.Count}"
                + (roles.Contains(EmployeeRole.Buyer) ? (OpeningBuyerPlanReady(shop) ? " · 已安排采购" : " · 待安排采购") : "");
            return guide;
        }

        internal const string RestaurantOpeningServiceHint = "把大米和配菜放进冰箱。来客后在柜台查看订单，备料、下锅、出锅上菜。顾客吃完后收桌并收款。可以先到「员工」招聘服务员和厨师。";

        internal bool ExpansionStorefrontComplete(int number)
        {
            var shop = ExpansionStorefront(number);
            return shop != null && (shop.RetailKind != "restaurant" || HasRestaurantDrinkShelf(shop)) && Ledger.Entries.Any(entry =>
                FinanceReport.IsShopSale(entry, shop.Place.Id, shop.SalesAccountId));
        }

        private bool HasPendingStorefront => PendingIsNewShop && PendingLease != null
            && !IsDedicatedVenue(City.GetPlace(PendingLease.PlaceId));

        private ShopListing CheapestExpansionListing() => Listings.Where(listing =>
        {
            if (LeaseTermsBlocker(listing, newShop: true, agency: null) != null) return false;
            var place = ListingPlace(listing);
            return DistrictAccessBlocker(place) == null && AgencyFor(place) is Place agency && DistrictAccessBlocker(agency) == null;
        }).OrderBy(listing => LeaseCost(listing, newShop: true)).FirstOrDefault();

        private bool SecondStorefrontQuestReady() => ExpansionStorefront(2) != null || HasPendingStorefront
            || (CheapestExpansionListing() is ShopListing listing && Ledger.CanAfford(2 * LeaseCost(listing, newShop: true)));

        internal QuestDef ExpansionOpeningGuide(QuestDef source, int number)
        {
            var shop = ExpansionStorefront(number);
            if (shop == null)
            {
                if (!HasPendingStorefront)
                {
                    if (number != 2 || CheapestExpansionListing() is not ShopListing listing) return source;
                    var search = source.Copy();
                    search.GuideDestination = AgencyFor(ListingPlace(listing));
                    return search;
                }
                var pending = source.Copy();
                pending.Target = QuestTarget.Bed;
                pending.Short = ShopHandoverText(PendingLease.StartDay);
                pending.Hint = $"已签下{City.GetPlace(PendingLease.PlaceId).Name}。{pending.Short}，交房后可以进店装修。";
                return pending;
            }

            var guide = source.Copy();
            string kind = shop.RetailKind;
            string name = ShopTitle(shop);
            guide.Progress = _ => name;
            QuestDef Step(QuestTarget target, string next, string hint, Place destination = null)
            {
                guide.Target = target;
                guide.Short = next;
                guide.Hint = $"{name} · {RetailShopTypes.Name(kind)}\n{hint}";
                guide.GuideDestination = destination;
                return guide;
            }

            var required = OpeningFurniture(kind);
            var missing = MissingOpeningFurniture(shop);
            if (missing.Length > 0)
                return Step(QuestTarget.Mall, "买" + string.Join("、", missing.Select(OpeningFurnitureName)),
                    "还需购买\n" + string.Join("\n", missing.Select(id => FurnitureCatalog.Get(id).Name + " ×1 · " + FurnitureCatalog.Get(id).Price))
                    + (kind == "restaurant" && missing.Contains(FurnitureCatalog.Shelf) ? "\n货架任选一种规格。" : "")
                    + $"\n送货时选「{name}」。");

            var unplaced = required.Where(id => !OpeningFurniturePlaced(shop, id)).ToArray();
            if (unplaced.Length > 0)
                return Step(QuestTarget.Renovate, "到新店装修，摆好开店设施",
                    "在店铺「装修」中摆放\n" + string.Join("\n", unplaced.Select(OpeningFurnitureName))
                    + "\n把设施送到新店后再摆放。装修当天歇业。", shop.Place);

            if (kind == "restaurant" && !HasRestaurantDrinkShelf(shop))
                return Step(QuestTarget.Stock, "把货架设为「饮料」", RestaurantDrinkShelfHint, shop.Place);

            if (shop.Sim.Queue.Count > 0 || kind == "restaurant" && _specialBusinesses.TryGetValue(shop.Place.Id, out var service)
                && service.Orders.Any(order => order.Stage is DiningStage.Cooking or DiningStage.Ready or DiningStage.Eating or DiningStage.Checkout))
                return FirstSale();

            var shelves = shop.Layout.Where(item => FurnitureCatalog.Get(item.Id)?.Fixture == FixtureKind.Shelf)
                .Select(item => ShelfKinds.Get(item.Item)).Where(shelf => shelf != null)
                .Where(shelf => kind == "secondhand" ? shelf.Id == "secondhand"
                    : kind == "computer" ? shelf.IsParts : shelf.GiftIds.Any(id => RetailShopTypes.Sells(kind, id)))
                .Distinct().ToArray();
            if (kind != "restaurant" && shelves.Length == 0)
            {
                string category = kind == "secondhand" ? "二手转售品" : kind == "computer" ? "电脑配件" : RetailShopTypes.Name(kind) + "商品";
                return Step(QuestTarget.Stock, "设置新店的货架品类",
                    $"在「库存」选择货架，或走到货架前按 F，再选「换品类」，陈列{category}。", shop.Place);
            }

            if (kind == "restaurant")
            {
                bool kitchenReady = KitchenRice(shop) >= .25 && RecipeIngredients.Any(id => KitchenIngredientCount(shop, id) > 0);
                if (!kitchenReady)
                {
                    var food = new[] { shop.Fridge.Food, shop.Locker.Food, shop.Temp.Food, Backpack.Food };
                    if (food.Sum(store => store.RiceKg) >= .25
                        && RecipeIngredients.Any(id => food.Sum(store => store.IngredientCount(id)) > 0))
                        return Step(QuestTarget.Stock, "把大米和配菜放进新店冰箱",
                            "到冰箱旁按 F，把大米和至少一种配菜从背包或后仓放进冰箱。临时存放的食材可先取回。", shop.Place);
                    if (IncomingItems().Any(item => item.Kind == CourierItemKind.Rice
                        || item.Kind == CourierItemKind.Ingredient && RecipeIngredients.Contains(item.Id)))
                        return Step(QuestTarget.Stock, "等待食材送到新店",
                            "货车正在送货。到货后检查冰箱里的大米和配菜，少的再补。", shop.Place);
                    return Supply("去农贸市场买大米和配菜", "买大米和至少一种配菜，送到这家餐馆后放进冰箱。");
                }
            }
            else if (kind != "secondhand" && !shelves.Any(shelf => shop.Sim.ShelfFillOf(shelf).Total > 0))
            {
                var stockedShelf = kind == "computer"
                    ? ShelfKinds.All.FirstOrDefault(shelf => shelf.IsParts && PartCatalog.InCategory(shelf.Category).Any(part => shop.Sim.Stock.CountAll(part) > 0))
                    : ShelfKinds.GoodsKinds.FirstOrDefault(shelf => shelf.GiftIds.Any(id => RetailShopTypes.Sells(kind, id)
                        && shop.Sim.Stock.GoodsCount(id) > 0));
                if (stockedShelf != null)
                    return Step(QuestTarget.Stock, $"把货架设为「{stockedShelf.Name}」",
                        "店里已有这类商品。设置对应货架后，后仓的商品会自动陈列。", shop.Place);
                bool hasGoodsToUnload = kind == "computer"
                    ? Cargo.Lines().Any()
                    : new[] { Backpack, shop.Locker, shop.Temp }.Any(store => store.GiftLines
                        .Any(line => RetailShopTypes.Sells(kind, line.Key)));
                if (hasGoodsToUnload)
                    return Step(QuestTarget.Stock, "把首批货放进新店库存",
                        kind == "computer" ? "到新店打开背包，把配件卸到店里，再设置对应货架。"
                            : "到新店「库存」选择从背包或后仓上架。临时存放的货可先取回，再设置对应货架。", shop.Place);
                if (IncomingItems().Any(item => kind == "computer" ? item.Kind == CourierItemKind.Part
                    : (item.Kind == CourierItemKind.Gift || item.Kind == CourierItemKind.Goods)
                        && RetailShopTypes.Sells(kind, item.Id)))
                    return Step(QuestTarget.Stock, "等待首批货送到新店",
                        "货车正在送货。到货后在新店「库存」检查对应货架。", shop.Place);
                return Supply("进新店的第一批货", $"采购和货架对应的商品（{string.Join("、", shelves.Select(shelf => shelf.Name))}），自提入库或送到「{name}」。");
            }

            if (shop.RenovationDay >= Clock.Today.TotalDays)
                return Step(QuestTarget.Bed, "装修结束，等明天开业", "这家店今天歇业装修。次日营业时回店接待顾客。");

            if (!OpeningStaffDone(shop)) return OpeningStaffGuide(guide, shop);

            if (kind == "secondhand" && !shelves.Any(shelf => shop.Sim.ShelfFillOf(shelf).Total > 0))
            {
                bool hasOldGoods = _specialBusinesses.TryGetValue(shop.Place.Id, out var resale)
                    && resale.Used.Any(item => item.Owned && !item.Sold);
                return hasOldGoods
                    ? Step(QuestTarget.Counter, "在新店鉴定并上架旧物",
                        "打开「收购鉴定台」，鉴定已收下的旧物，再定价上架。修复中的旧物要等修完。", shop.Place)
                    : Step(QuestTarget.Counter, "在新店收购第一批旧物",
                        "营业时打开「收购鉴定台」，接待卖家。买下旧物后鉴定、定价，再上架。", shop.Place);
            }

            return FirstSale();

            QuestDef FirstSale() => Step(QuestTarget.Counter, "在新店完成第一单",
                kind == "restaurant" ? RestaurantOpeningServiceHint
                    : "营业时留在店里，顾客排队后到收银台结账。也可以安排收银员值班。", shop.Place);

            QuestDef Supply(string next, string hint)
            {
                if (ShopSupplyMarketId(kind) is string marketId)
                    return Step(QuestTarget.Market, next, hint, PurchaseDestination(City.GetPlace(marketId)));
                return Step(QuestTarget.Stock, next, hint, shop.Place);
            }

            IEnumerable<CourierItem> IncomingItems() => Couriers
                .Where(run => run.ToShop && (run.ShopId ?? MainShop.Place.Id) == shop.Place.Id)
                .SelectMany(run => run.Items);
        }

        public string OpeningRewardText(string id)
        {
            if(id=="rent")return null;
            string kind=OpeningRewardKind(id)??RetailKind;
            if(!HasShop&&OpeningRewardKind(id)==null)return "按签约时选择的经营类型发放";
            return kind=="restaurant"?"大米 2 千克 + 青菜、鸡蛋、五花肉各 4 份"
                :kind=="clothing"?"当季基础服装两款，各 3 件"
                :kind=="models"?"常规模型两款，各 3 件"
                :kind=="secondhand"?"两件已鉴定的二手好货"
                :kind=="computer"?"入门 CPU + 入门显卡":kind==PetShopCatalog.ShopKind?"猫粮、狗粮、猫砂各 3 袋，逗猫棒、软球各 1 件":"本店品类商品两款，各 3 件";
        }

        internal void GrantOpeningReward(string id,bool legacy=false)
        {
            if(id=="rent"||OpeningRewardKind(id)!=null)return;
            string kind=RetailKind;
            Flags.Add("tutorial:reward:"+id+":"+kind);
            // Old computer saves already received their starter stock.
            if(legacy&&kind=="computer")return;
            if(kind=="restaurant")
            {
                UnpackRestaurantIngredient(CurrentShop, new CourierItem { Kind = CourierItemKind.Rice, Kg = 2 });
                foreach (string ingredient in RecipeIngredients)
                    UnpackRestaurantIngredient(CurrentShop, new CourierItem { Kind = CourierItemKind.Ingredient, Id = ingredient, Count = 4 });
            }
            else if(kind=="secondhand")
            {
                var state=SpecialBusiness(CurrentShop.Place.Id);
                for(int product=0;product<2;product++)
                {
                    var item=new UsedItem{Id=state.NextId++,Product=product,Owned=true,Inspected=true,Condition=UsedCondition.Good,Claim=UsedCondition.Good};
                    item.Price=UsedValue(item);state.Used.Add(item);
                }
            }
            else if(kind=="computer")
            {
                PlayerShop.Stock.ForceAdd(PartCatalog.Get("cpu0"),true,1);
                PlayerShop.Stock.ForceAdd(PartCatalog.Get("gpu0"),true,1);
            }
            else if(kind==PetShopCatalog.ShopKind)
            {
                AddStarterPetStock();
            }
            else
            {
                var goods=kind=="clothing"?ClothingProducts.All.OrderByDescending(g=>ClothingProducts.IsInSeason(g.Id,Clock.Now.TotalMinutes)).ThenBy(g=>g.Price.Yuan)
                    :Gifts.All.Where(g=>RetailShopTypes.Sells(kind,g.Id)&&!g.LimitedEdition).OrderBy(g=>g.Price.Yuan);
                foreach(var good in goods.Take(2))PlayerShop.Stock.ForceAddGoods(good.Id,3);
            }
            if(legacy)Say("已补齐原开店任务的"+RetailShopTypes.Name(kind)+"礼包："+OpeningRewardText(id)+"。仅补发一次。");
        }

        private void AddStarterPetStock()
        {
            PlayerShop.Stock.ForceAddGoods(PetShopCatalog.CatFood, 3);
            PlayerShop.Stock.ForceAddGoods(PetShopCatalog.DogFood, 3);
            PlayerShop.Stock.ForceAddGoods(PetShopCatalog.BasicLitter, 3);
            PlayerShop.Stock.ForceAddGoods(PetShopCatalog.Wand, 1);
            PlayerShop.Stock.ForceAddGoods(PetShopCatalog.Ball, 1);
        }

        private void MigrateOpeningRewards()
        {
            if(!HasShop||IsDedicatedVenue(CurrentShop.Place))return;
            if(QuestCompleted("mall")&&OpeningRewardKind("mall")==null)GrantOpeningReward("mall",true);
        }
    }
}
