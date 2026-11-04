# 当前完整工程联合检查（2026-11-04）

当前工作区的Assets、Packages、ProjectSettings完整同步到隔离工程`D:/UnityProjects/PCShop/_ci_retro_portraits`，Unity 2022.3.6f1 EditMode检查。没有读取或覆盖正式玩家存档。该镜像此前只验头像，本轮已重新同步完整工程，包含MCN、网购与品牌主线全部未提交改动。

本次返回：total=894，passed=846，failed=48，skipped=0，result=Failed(Child)。原始记录：[59-current-archive-tests.xml](59-current-archive-tests.xml)。

后续核对发现7个品牌故事用例的共同夹具没有满足新MCN“先认识再合作”的前置，已仅在`BrandStoryTests.Make`为故事场景准备已认识人物，不放松运行时规则。随后重跑下表8组，最终**64/64通过、0失败**：[最终专项原始报告](59-current-archive-final-tests.xml)。其中BrandStoryTests为16/16通过；其余组结果与下表一致。联合初跑其余41个失败仍保留，未借此宣称整套894项已重跑全绿。

当前完整工程图形检查也通过：`SMOKE RESULT code=0 errors=0`，6张图册和8张资料/对话截图已重新生成，复核KOL/陆栀/温岚/宁汐图册及莫文宁实际对话画面。纹理对象合计23,674,560字节，仍为同一导入配置；见[本轮图形记录](59-current-archive-graphics.txt)。

最初尝试全量测试，三年经营压力用例`CampaignTests.ThreeYears_WithEventsEnabled_RunsWithoutErrors`运行数分钟仍未完成，因此结束该次尝试；排除该用例的正则筛选仍匹配其父测试组，未能有效排除，随即停止。最终改为明确指定82个测试组，未纳入CampaignTests与EconomyCalibrationTests等未被该组名列表覆盖的测试。没有把被停止的运行算成通过，也不宣称完成全仓库所有测试。

## 新增与本次主要系统

| 测试组 | 通过 | 失败/其他 |
| --- | --- | --- |
| McnIntroductionTests | 8 | 0 |
| SimplifiedShoppingTests | 5 | 0 |
| DistrictCommerceTests | 7 | 0 |
| RetroPortraitTests | 3 | 0 |
| BrandBusinessTests | 16 | 0 |
| BrandStoryTests | 9 | 7 |
| BrandStoryUiTests | 1 | 0 |
| NarrativeContentTests | 8 | 0 |

## 联合运行中未通过的用例

以下为联合初跑实际结果，不将失败一概认定为无关旧问题。7项BrandStoryTests已通过上述夹具准备和专项重跑解决，其他条目仍是待复查项。文档归档任务未为通过测试修改既有玩法或删除断言；历史相似失败见既有专题，是否同根因应单独复查。

- `PCShopSell.Tests.BrandStoryTests.EndingContractDoesNotFakeFinishedRetestOrAwardRelationship`：Expected: True But was: False
- `PCShopSell.Tests.BrandStoryTests.PreparationIsLocalConsumesSelectedStockOnceAndDoesNotPayProfit`：Expected: True But was: False
- `PCShopSell.Tests.BrandStoryTests.PromoFollowupUsesOnlySelectedShopRevenueAndCompletesOnceAfterSave`：Expected: True But was: False
- `PCShopSell.Tests.BrandStoryTests.ReadingOffersAndProfilesDoesNotStartStoryOrRevealConclusion`：Expected: 1 But was: 0
- `PCShopSell.Tests.BrandStoryTests.RetestRequiresTwoSamplesAndARealDay`：Expected: True But was: False
- `PCShopSell.Tests.BrandStoryTests.StoryCopiesDoNotMutateLiveProgressAndOldSaveGetsEmptyRecords`：Expected: True But was: False
- `PCShopSell.Tests.BrandStoryTests.ZeroSalesIsAnHonestCompleteOutcome`：Expected: True But was: False
- `PCShopSell.Tests.CityActivityTests.FutureAppointmentWithEmptyJsonSeatRouteStillWalksToTheVenueAfterLoad`：场所已经打烊，尚未消费 Expected: Running But was: Cancelled
- `PCShopSell.Tests.CityActivityTests.RealTravelMealAndReloadSettleEachStageOnce`：场所已经打烊，尚未消费 Expected: Running But was: Cancelled
- `PCShopSell.Tests.CityLayoutV7Tests.ConfirmedPlanHasFourAgencyDistrictsAndNoStarterShopStation`：Expected: 8 But was: 14
- `PCShopSell.Tests.CityLayoutV7Tests.LayoutMigrationDoesNotReplayCashOrDuplicateAssetsAndThenIsIdempotent`：Expected: 8 But was: 14
- `PCShopSell.Tests.CityLayoutV7Tests.MetroTravelsToChosenStationRatherThanLastEnumeratedStation`：Expected: True But was: False
- `PCShopSell.Tests.CityLayoutV7Tests.ResidentialSceneryHasMultipleRowsAndDoesNotCollapseToFourApartmentBlocks`：s_oldhomes Expected: greater than or equal to 45 But was: 19
- `PCShopSell.Tests.CityStoryTests.LongNpcTripUsesSameOrdinaryTransportForEstimateAndActualSavedRoute`：Expected: Taxi But was: Walk
- `PCShopSell.Tests.CustomDestinationTests.MapSelectionPinsWithoutTravelAndMainButtonTravelsAfterJsonReload`：String lengths are both 5. Strings differ at index 0. Expected: "自定目的地" But was: "选择目的地" -----------^
- `PCShopSell.Tests.EnergyPayrollTests.FatigueInterruptsRealWorkTravelAndAnActiveExclusiveActivityOnlyOnce`：Expected: not null But was: null
- `PCShopSell.Tests.FixtureTests.Catalog_HasShelfTiers_AndTheOtherFixtures`：保留三档通用货架，新增服装衣架与折叠陈列架 Expected: 5 But was: 3
- `PCShopSell.Tests.HomeTests.Courier_BigOrder_SplitsAcrossCars`：Expected: True But was: False
- `PCShopSell.Tests.HomeTests.MallCart_SeveralTrucksArrivingTogether_OnePopup`：进了临时存放要说清楚能放几天 Expected: True But was: False
- `PCShopSell.Tests.InteractionFlowTests.RecruitWithAssignment_RejectsOccupiedRole_WithoutHiringOrRemovingCandidate`：Expected: False But was: True
- `PCShopSell.Tests.MarkupRangeTests.ShopSliderAndRestaurantMenuReachThree_AndMenuShowsZeroIntent`：Expected: 3 But was: 1.1499999999999999d
- `PCShopSell.Tests.PlayerWorkTests.AttendedWeeklyWageGetsBankruptcyGraceUntilClaimDeadline`：Expected: True But was: False
- `PCShopSell.Tests.PlayerWorkTests.BrokeWorkerCanFinishTheWeekWithWorkMealsAndSleep_WithoutBuyingStockOrFood`：Expected: True But was: False
- `PCShopSell.Tests.PlayerWorkTests.FullWeekPaysOnce_AfterClaim_AndAllowsRenewal`：Expected: True But was: False
- `PCShopSell.Tests.PlayerWorkTests.LastDayAbsenceStillClosesPayroll_AndCanClaimAwayFromShop`：Expected: True But was: False
- `PCShopSell.Tests.PlayerWorkTests.LastNightPaysOnlyAfterNextMorning_AndMidnightJsonKeepsCorrectShiftDay`：Expected: True But was: False
- `PCShopSell.Tests.PlayerWorkTests.LatenessReducesOnlyTodaysWage_WithNoExtraDebt(1,141)`：工资周结，迟到不会直接扣现金 Expected: ¥0 But was: ¥141
- `PCShopSell.Tests.PlayerWorkTests.LatenessReducesOnlyTodaysWage_WithNoExtraDebt(30,141)`：工资周结，迟到不会直接扣现金 Expected: ¥0 But was: ¥141
- `PCShopSell.Tests.PlayerWorkTests.LatenessReducesOnlyTodaysWage_WithNoExtraDebt(31,132)`：工资周结，迟到不会直接扣现金 Expected: ¥0 But was: ¥132
- `PCShopSell.Tests.PurchasePlanTests.PartialCash_BuysInListOrderAndReturnsChangeOnceAfterSave`：Expected: ¥1,069 But was: ¥3,039
- `PCShopSell.Tests.PurchasePlanTests.PartialCash_SkipsUnaffordableEarlyItemButStillBuysLaterCheapItem`：String lengths are both 4. Strings differ at index 3. Expected: "cpu0" But was: "cpu2" --------------^
- `PCShopSell.Tests.PurchasePlanTests.PostingAnotherAdRetainsPreviousCandidates_AndAddsMoreTomorrow`：Expected: 4 But was: 2
- `PCShopSell.Tests.ShopFootfallTests.SpecializedShopsUseFootfallForVisitFrequency("secondhand")`：Expected: greater than 0 But was: -1
- `PCShopSell.Tests.SocialTests.Appointment_BecomesRealTravelAndOnlyCompletedOutingGrantsRelation`：场所已经打烊，尚未消费 | minute=8519 start=8340 activity=Gathering li phase=Waiting selected=Walk place=life_restaurant task=Waiting activityId=activity_1 route= arrival= mode= Expected: Running But was: Cancelled
- `PCShopSell.Tests.SocialTests.EnteringOwnersStall_OnlyMakesYouFamiliar_ContactIsAskedForInPerson`：Expected: null But was: "还不够熟（交情 30）"
- `PCShopSell.Tests.SocialTests.MissedAppointmentRecordsNoShowWithoutFalseCompletionOrMealCharge`：| minute=8355 start=8340 activity=Gathering li phase=Waiting selected=Walk place=life_restaurant task=Waiting activityId=activity_1 route= arrival= mode= Expected: NoShow But was: Gathering
- `PCShopSell.Tests.SocialTests.SaveLoad_KeepsSocialState`：Expected: True But was: False
- `PCShopSell.Tests.SocialTests.SmallTalk_OncePerDay_GainsRelation_AndTakesTenMinutes`：Expected: 10 But was: 0
- `PCShopSell.Tests.UnifiedClockAndWeightTests.LegacyRiceStorageAndCourierConvertOnce_WithoutChangingCashOrFood`：Expected: 3 But was: 7.0d
- `PCShopSell.Tests.WorkforceTests.InvalidProcurement_DoesNotChargeReserveVehicleOrCreateGoods("load")`：Expected: not null But was: null
- `PCShopSell.Tests.WorkforceTests.InvalidProcurement_DoesNotChargeReserveVehicleOrCreateGoods("quota")`：Expected: not null But was: null
- `PCShopSell.Tests.WorkforceTests.Transfer_CanCancel_AndCannotOverwriteOccupiedRole`：Expected: False But was: True
- `PCShopSell.Tests.WorkshopRegressionTests.AssemblerRespectsShiftAndKeepsExperienceAcrossSave`：Expected: greater than 0 But was: 0
- `PCShopSell.Tests.WorkshopRegressionTests.CompletedAssemblyPaysAfterElapsedTime_AndConsumesOnlyOnce`：Expected: True But was: False
- `PCShopSell.Tests.WorkshopRegressionTests.EmployeeCanAssembleWhileOwnerIsPresent_AndResumeAfterLoad`：System.ArgumentOutOfRangeException : Index was out of range. Must be non-negative and less than the size of the collection. Parameter name: index
- `PCShopSell.Tests.WorkshopRegressionTests.FiringCashierDoesNotResetDedicatedAssembly`：Expected: greater than 0 But was: 0
- `PCShopSell.Tests.WorkshopRegressionTests.ReservationPreservesWealth_AndSurvivesSaveLoad`：Expected: True But was: False
- `PCShopSell.Tests.WorkshopRegressionTests.ShopHoursDropdown_SurvivesLiveRefreshAndCommitsSelection`：Expected: 555 But was: 600

## 其他核对

- `tools/check-lake-city.py`通过：91地点、四家中介、旧ID、连通、建筑/道路/湖面及南北最短路规则；直接统计当前JSON为版本14、1045节点、1062边。
- 头像清单121条全部存在，源PNG哈希匹配，121个不同图像；6位颜值修订最终源图与资源一致，保留对应Unity meta及512导入上限。
- 当前源码/资源清单见[文件索引](../snapshot-2026-11-04-files.json)；新增Unity图片和代码的meta已核对，无超过GitHub单文件100MiB限制的文件。
- 差异格式检查与当前变更文件常见凭据模式检查通过，未输出凭据内容。
- 历史图形截图/专项测试仍保留原执行范围，不将它们加成当前测试数量。最新图形检查见同目录`59-current-archive-graphics.txt`（若存在）。
- 未新建Windows/WebGL构建或GitHub Release。
