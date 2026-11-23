# 154 本轮收尾与测试欠账（2026-11-23）

本文记录这一轮的改动、当前验证结果和尚未处理的事项，供后续接手。

## 1. 本轮完成

### 1.1 逻辑修复
- 赛车爱好者 id 改为 `resident_gaobo / resident_hexin / resident_sujun / resident_zengyu`，与居民名册一致（`CityLife.Racing.cs`）。
- 读档幽灵状态：Unity `JsonUtility` 会把空的可序列化类字段写成默认对象，读档后出现“假的”活动。现在用有效标记判断：刮刮乐 `Id > 0`、林某贷款 `DueDay > 0`，否则恢复为 null（`GameSession.Events.cs`、`GameSession.EventMoneyBatch.cs`）。
- 网购优惠券抵扣后实付为 0 时不再扣款，避免 `Ledger.TryDebit` 对 0 金额抛异常（`GameSession.CityProcurement.cs`）。
- `SaveStore` 读档任何异常都会改试下一个候选文件或 `.bak`，原文件保留，并输出 `Debug.LogWarning`。
- `companyName` 改为 `KnifeStudio`。本机存档已复制到 `LocalLow\KnifeStudio\PCShopSell`，旧目录未动。不写存档迁移代码（未发过关键包）。

### 1.2 立绘
- 运行时立绘只读 `Resources/PortraitsAnime/{id}`，缺失时回退 `PortraitsAnime/default`。
- 复古立绘移到 `Assets/Art/PortraitsRetro`，不再进入 Resources 和包体；只有编辑器工具引用它。
- 删除 `Assets/Art/PortraitsLegacy`（37 张 jpg，未被引用）。
- 新增 `res_bank_night`（林晓宁）动漫立绘，清单 `docs/art/npc-anime-portraits.json` 为 126/126。
- 立绘导入流程：`tools/import-anime-npc-portrait.py --id X --source PNG`，要求 3:4 PNG，比例 0.73–0.77；导入规则见 `Assets/Editor/NpcPortraitImportRules.cs`。

### 1.3 新增与修复的测试
- 新增：`SaveGhostStateTests`、`SaveStoreFallbackTests`、`VenueSponsorshipAndStoryTests`（场所赞助、感情线四阶段）。
- 修复到绿：`KolStreamingTests`、`RacingTests`、`FinanceEventBatchTests`、`RomanceConversationTests`、`McnSigningTest`。原因是玩法规则变了而测试没更新，规则变化包括：
  - KOL 直播至少 180 分钟；
  - 品牌在签约时自动分配；
  - 直播间 key 含时段；
  - 投诉赔偿为已付金额的 2 倍并整单回收积分；
  - 赛车对手数 10；
  - 通勤位只有一个，多辆车需要区域车库；
  - 感情线人物需先完成“赴约再聊”才能表白。

## 2. 全量 EditMode 验证结果

分批运行（整套一次跑会卡死），约 1300 个用例，**304 个失败**。其中只有 `AnimePortraitTests`、`RetroPortraitTests` 与本轮改动有关（46 个固定 NPC 还没有立绘）。其余是之前就存在的欠账。

会卡死、未运行的用例：
- `EconomyCalibrationTests.Bot_PlaysThreeYears`
- `CampaignTests` 中的三年模拟（`ThreeYears_WithEventsEnabled`）

### 2.1 失败按类统计

| 测试类 | 失败数 |
| --- | --- |
| UiCleanupTests | 38 |
| OnsitePlayTests | 33 |
| FishingTests | 25 |
| SocialEventBatchTests | 19 |
| ShopCompetitionTests | 13 |
| OutdoorBatchTests | 11 |
| ShopCoreGameplayTests | 11 |
| DistrictProgressionTests | 8 |
| PlayerWorkTests | 8 |
| FixtureTests | 7 |
| WorkforceTests | 7 |
| WorkshopRegressionTests | 6 |
| CityTravelTimingTests | 6 |
| FigureMotionTests、SteerTests、CityActivityTests、DistrictDetailTests | 各 5 |
| BusinessRegressionTests、GameSessionTests、PurchasePlanTests、ShopFootfallTests、CityLayoutV7Tests、DistrictCommerceTests、SocialTests | 各 4 |
| QuestTests、HomeTests、CompetitionContextTests、MarkupRangeTests | 各 3 |
| 其余 40 个测试类 | 各 1–2 |

完整的失败用例名见 `docs/154-failing-tests-2026-11-23.txt`（304 条）。

### 2.2 初步判断（尚未逐个确认）

**多半是测试过期**（设计改了，测试没跟上）：
- 商铺数量从 18 变为 8：`FixtureTests`、`GameSessionTests.RealLayout_MatchesDesign`、`CityLayoutV7Tests`。
- 价格与常量变化：服装 266（测试写 280），股票最大手数 275（测试写 200）。
- 初始货架与固定设施数量变化。

**可能是真 bug，需优先确认**：
- 电动车移动距离为 0（`CityTravelTimingTests` 的 EBike 用例）。
- 钓鱼场提示“暂时没有店员在岗，暂停营业”，导致钓鱼流程测试失败（`FishingTests`）。
- 睡过头后邀约未送达（`DistrictDetailTests`）。
- 同学聚餐、夜宵餐厅停在 Running，没有完成（`ClassmateDinnerTests`、`CityActivityTests`）。
- 小人动画：走路时鞋子穿过地面，`FigureMotionTests` 里有 null（`FigureMotionTests`）。
- 夜间和周末的银行贷款不可用（`BankTests`）。

## 3. 未完成与待决策

1. 46 个固定 NPC 没有动漫立绘：8 个医院医生，38 个 `life_leisure_staff_*`。游戏里会显示默认头像。建议分批生成，每批脸型要有区分，用 `res_auntie.png` 作风格参考，再用导入脚本和清单登记。
2. `Assets/Art/PortraitsRetro`（约 350MB）已无运行时引用，可以删除。目前仍保留为归档。
3. 304 个失败用例待分类：先区分“测试过期”和“真 bug”，只修真 bug 和关键流程，过期的统一更新。
4. 两个会卡死的三年模拟需要加超时或放进手动运行类别，否则无法跑完整套。
5. 之前审计列出的死代码、日历与营业时间配置重复，仍未处理。
6. 多年存档体积的模拟还没做。
7. `docs/art/anime-npc-portraits.md` 的数量描述还是 125，应为 126。

## 4. 测试运行方式

- 镜像工程 `F:\_ci`，通过 `F:\_ci\work\rt.ps1 '<过滤器>;<过滤器>' '<标签>'` 运行。该脚本先把 `Assets` 以 `/E /XO` 拷过去；被删除或移动的文件需要在 `F:\_ci` 里手动处理。
- 分批运行时，把测试类名按 `;` 分组后分别传入，每批设置超时。
- Windows 构建输出放在仓库外（`F:\PCShopSell_builds\`）。
