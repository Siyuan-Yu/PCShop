# 2026-11-02 当前实现提交文件清单

相对上一提交 `155da0b`，本次归档共 168 个新增/修改项目文件，包含本清单。

完整规则、验证范围及打包边界见 [第33篇](33-update-2026-11-02-gameplay-and-warehouses.md)。不包含正式存档、Builds、Library、Temp、日志和本机凭据。

## 编辑器工具（9项）

- `Assets/Editor/CityArtReview.cs`
- `Assets/Editor/CityArtReview.cs.meta`
- `Assets/Editor/CityModelCapture.cs`
- `Assets/Editor/CityModelCapture.cs.meta`
- `Assets/Editor/GameSceneBuilder.Interactions.cs`
- `Assets/Editor/GameSceneBuilder.PurchasePlan.cs`
- `Assets/Editor/GameSceneBuilder.Workforce.cs`
- `Assets/Editor/GameSceneBuilder.cs`
- `Assets/Editor/PlayModeSmoke.cs`

## 测试（32项）

- `Assets/Tests/EditMode/BuildingBoundsTests.cs`
- `Assets/Tests/EditMode/BuildingBoundsTests.cs.meta`
- `Assets/Tests/EditMode/BuyerHistoryTests.cs`
- `Assets/Tests/EditMode/BuyerHistoryTests.cs.meta`
- `Assets/Tests/EditMode/CampaignTests.cs`
- `Assets/Tests/EditMode/CheckoutPatienceTests.cs`
- `Assets/Tests/EditMode/CheckoutPatienceTests.cs.meta`
- `Assets/Tests/EditMode/CityClothingTests.cs`
- `Assets/Tests/EditMode/CityLayoutV7Tests.cs`
- `Assets/Tests/EditMode/CityRetailTests.cs`
- `Assets/Tests/EditMode/CityTravelTimingTests.cs`
- `Assets/Tests/EditMode/ClassmateDinnerTests.cs`
- `Assets/Tests/EditMode/ClassmateDinnerTests.cs.meta`
- `Assets/Tests/EditMode/CompetitionContextTests.cs`
- `Assets/Tests/EditMode/CompetitionContextTests.cs.meta`
- `Assets/Tests/EditMode/CustomDestinationTests.cs`
- `Assets/Tests/EditMode/CustomDestinationTests.cs.meta`
- `Assets/Tests/EditMode/EmployeeAssignmentTests.cs`
- `Assets/Tests/EditMode/MultiShopTests.cs`
- `Assets/Tests/EditMode/PCShopSell.Tests.EditMode.asmdef`
- `Assets/Tests/EditMode/PackTests.cs`
- `Assets/Tests/EditMode/PurchasePlanTests.cs`
- `Assets/Tests/EditMode/ShopDockInteractionTests.cs`
- `Assets/Tests/EditMode/ShopDockInteractionTests.cs.meta`
- `Assets/Tests/EditMode/ShopManageTests.cs`
- `Assets/Tests/EditMode/VenueBusinessTests.cs`
- `Assets/Tests/EditMode/VenueBusinessTests.cs.meta`
- `Assets/Tests/EditMode/WarehouseBoxInteractionTests.cs`
- `Assets/Tests/EditMode/WarehouseBoxInteractionTests.cs.meta`
- `Assets/Tests/EditMode/WarehouseCapacityTests.cs`
- `Assets/Tests/EditMode/WarehouseCapacityTests.cs.meta`
- `Assets/Tests/EditMode/WorkshopRegressionTests.cs`

## 生成与校验工具（4项）

- `tools/build-city-v7.py`
- `tools/check-city-v7.py`
- `tools/densify-city.py`
- `tools/plan-four-agencies.py`

## 文档、验证与预览图（17项）

- `docs/00-overview.md`
- `docs/08-current-status.md`
- `docs/10-current-content.md`
- `docs/33-update-2026-11-02-gameplay-and-warehouses.md`
- `docs/city-life-delivery.md`
- `docs/previews/city-four-agency-jurisdictions-v1.png`
- `docs/previews/city-six-agency-jurisdictions-v1.png`
- `docs/previews/current-models/models_01_city.png`
- `docs/previews/current-models/models_02_old_neighborhood.png`
- `docs/previews/current-models/models_03_apartments.png`
- `docs/previews/current-models/models_04_villas.png`
- `docs/previews/current-models/models_05_bar.png`
- `docs/previews/current-models/models_05_ktv.png`
- `docs/previews/current-models/models_05_restaurant.png`
- `docs/snapshot-2026-11-02-gameplay-files.md`
- `docs/validation/33-current-results.txt`
- `docs/validation/33-warehouse-box-tests.xml`

## 项目入口与其他（1项）

- `README.md`

## 运行时与UI（50项）

- `Assets/Scripts/Game/Procedural/CityLookProfile.cs`
- `Assets/Scripts/Game/Runtime/CityArchitecture.ModernVilla.cs`
- `Assets/Scripts/Game/Runtime/CityArchitecture.ModernVilla.cs.meta`
- `Assets/Scripts/Game/Runtime/CityArchitecture.Reference.cs`
- `Assets/Scripts/Game/Runtime/CityArchitecture.Reference.cs.meta`
- `Assets/Scripts/Game/Runtime/CityArchitecture.cs`
- `Assets/Scripts/Game/Runtime/CityArchitecture.cs.meta`
- `Assets/Scripts/Game/Runtime/CityVegetation.cs`
- `Assets/Scripts/Game/Runtime/CityVegetation.cs.meta`
- `Assets/Scripts/Game/Runtime/CityVenueView.cs`
- `Assets/Scripts/Game/Runtime/CrowdView.Ambient.cs`
- `Assets/Scripts/Game/Runtime/CrowdView.cs`
- `Assets/Scripts/Game/Runtime/DayNightLighting.cs`
- `Assets/Scripts/Game/Runtime/GameController.CityCamera.cs`
- `Assets/Scripts/Game/Runtime/GameController.cs`
- `Assets/Scripts/Game/Runtime/HomeRoomView.cs`
- `Assets/Scripts/Game/Runtime/Mats.cs`
- `Assets/Scripts/Game/Runtime/ShopBeacon.cs`
- `Assets/Scripts/Game/Runtime/ShopInteriorView.cs`
- `Assets/Scripts/Game/Runtime/StreetWorldBuilder.City.cs`
- `Assets/Scripts/Game/Runtime/StreetWorldBuilder.Planned.cs`
- `Assets/Scripts/Game/Runtime/StreetWorldBuilder.ReferenceArchitecture.cs`
- `Assets/Scripts/Game/Runtime/StreetWorldBuilder.ReferenceArchitecture.cs.meta`
- `Assets/Scripts/Game/Runtime/StreetWorldBuilder.ReferenceLandscape.cs`
- `Assets/Scripts/Game/Runtime/StreetWorldBuilder.ReferenceLandscape.cs.meta`
- `Assets/Scripts/Game/Runtime/StreetWorldBuilder.cs`
- `Assets/Scripts/Game/UI/AgencyBuildingGlow.cs`
- `Assets/Scripts/Game/UI/AgencyStreetMap.cs`
- `Assets/Scripts/Game/UI/GameUi.Agency.cs`
- `Assets/Scripts/Game/UI/GameUi.AgencyMap.cs`
- `Assets/Scripts/Game/UI/GameUi.Cart.cs`
- `Assets/Scripts/Game/UI/GameUi.CityLifePanels.cs`
- `Assets/Scripts/Game/UI/GameUi.CityNavigation.cs`
- `Assets/Scripts/Game/UI/GameUi.Employees.cs`
- `Assets/Scripts/Game/UI/GameUi.FoodExpiry.cs`
- `Assets/Scripts/Game/UI/GameUi.Home.cs`
- `Assets/Scripts/Game/UI/GameUi.Panels.cs`
- `Assets/Scripts/Game/UI/GameUi.Quests.cs`
- `Assets/Scripts/Game/UI/GameUi.Retail.cs`
- `Assets/Scripts/Game/UI/GameUi.ShelfPricing.cs`
- `Assets/Scripts/Game/UI/GameUi.Shop.cs`
- `Assets/Scripts/Game/UI/GameUi.ShopBuyers.cs`
- `Assets/Scripts/Game/UI/GameUi.ShopBuyers.cs.meta`
- `Assets/Scripts/Game/UI/GameUi.ShopShelf.cs`
- `Assets/Scripts/Game/UI/GameUi.VenueBusiness.cs`
- `Assets/Scripts/Game/UI/GameUi.VenueBusiness.cs.meta`
- `Assets/Scripts/Game/UI/GameUi.VenueServices.cs`
- `Assets/Scripts/Game/UI/GameUi.Wholesale.cs`
- `Assets/Scripts/Game/UI/GameUi.cs`
- `Assets/Scripts/Game/UI/Ui.cs`

## Core逻辑（52项）

- `Assets/Scripts/Core/Home/ClothingProducts.cs`
- `Assets/Scripts/Core/Home/Furniture.cs`
- `Assets/Scripts/Core/Home/RetailProducts.cs`
- `Assets/Scripts/Core/Inventory/ShelfPlanner.cs`
- `Assets/Scripts/Core/Inventory/Stock.cs`
- `Assets/Scripts/Core/Session/CityLife.cs`
- `Assets/Scripts/Core/Session/EventCatalog.cs`
- `Assets/Scripts/Core/Session/GameSession.BusinessLife.cs`
- `Assets/Scripts/Core/Session/GameSession.CityActivities.cs`
- `Assets/Scripts/Core/Session/GameSession.CityAssets.cs`
- `Assets/Scripts/Core/Session/GameSession.CityLayoutMigration.cs`
- `Assets/Scripts/Core/Session/GameSession.CityPeople.cs`
- `Assets/Scripts/Core/Session/GameSession.CityPurchaseTravel.cs`
- `Assets/Scripts/Core/Session/GameSession.CityRetail.cs`
- `Assets/Scripts/Core/Session/GameSession.CityStories.cs`
- `Assets/Scripts/Core/Session/GameSession.EmployeeAssignments.cs`
- `Assets/Scripts/Core/Session/GameSession.Events.cs`
- `Assets/Scripts/Core/Session/GameSession.Fixtures.cs`
- `Assets/Scripts/Core/Session/GameSession.Goods.cs`
- `Assets/Scripts/Core/Session/GameSession.Growth.cs`
- `Assets/Scripts/Core/Session/GameSession.Home.cs`
- `Assets/Scripts/Core/Session/GameSession.Interactions.cs`
- `Assets/Scripts/Core/Session/GameSession.Pack.cs`
- `Assets/Scripts/Core/Session/GameSession.PurchaseCatalog.cs`
- `Assets/Scripts/Core/Session/GameSession.PurchaseHistory.cs`
- `Assets/Scripts/Core/Session/GameSession.PurchaseHistory.cs.meta`
- `Assets/Scripts/Core/Session/GameSession.Realty.cs`
- `Assets/Scripts/Core/Session/GameSession.Restock.cs`
- `Assets/Scripts/Core/Session/GameSession.Retail.cs`
- `Assets/Scripts/Core/Session/GameSession.Save.cs`
- `Assets/Scripts/Core/Session/GameSession.Shop.cs`
- `Assets/Scripts/Core/Session/GameSession.Shops.cs`
- `Assets/Scripts/Core/Session/GameSession.StaffCapacity.cs`
- `Assets/Scripts/Core/Session/GameSession.StaffCapacity.cs.meta`
- `Assets/Scripts/Core/Session/GameSession.Ventures.cs`
- `Assets/Scripts/Core/Session/GameSession.VenueBusiness.cs`
- `Assets/Scripts/Core/Session/GameSession.VenueBusiness.cs.meta`
- `Assets/Scripts/Core/Session/GameSession.VenueServices.cs`
- `Assets/Scripts/Core/Session/GameSession.Warehouses.cs`
- `Assets/Scripts/Core/Session/GameSession.Warehouses.cs.meta`
- `Assets/Scripts/Core/Session/GameSession.Workforce.cs`
- `Assets/Scripts/Core/Session/GameSession.Workshop.cs`
- `Assets/Scripts/Core/Session/GameSession.cs`
- `Assets/Scripts/Core/Session/OwnedShop.cs`
- `Assets/Scripts/Core/Session/Police.cs`
- `Assets/Scripts/Core/Session/Residents.cs`
- `Assets/Scripts/Core/Session/SaveData.CityLife.cs`
- `Assets/Scripts/Core/Session/SaveData.CityRetail.cs`
- `Assets/Scripts/Core/Session/SaveData.cs`
- `Assets/Scripts/Core/Session/Vehicles.cs`
- `Assets/Scripts/Core/Shop/ShopSimulation.cs`
- `Assets/Scripts/Core/World/CityLayout.cs`

## Unity资源与配置（3项）

- `Assets/Resources/Data/city_layout.json`
- `Assets/Settings/PCShopSell_Renderer.asset`
- `Assets/Settings/PCShopSell_URP.asset`
