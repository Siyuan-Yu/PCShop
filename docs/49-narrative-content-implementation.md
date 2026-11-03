# 人物与主线信息配置接入

日期：2026-11-03。按“把关键节点、关键人物及全部NPC的信息配出来”实施。本轮不打Windows包。

## 已接入游戏

- 120名正式固定身份NPC：20名关键人物、43名既有配角、57名新增居民。新增唐蔚、梁敬、沈晓，并补齐已有核心人物的信息。不含编辑器验收人物小游，也不限制以后动态招聘产生的额外员工。
- 每人三段单独撰写或保留的个人经历、日常话题、年龄、兴趣、目标、住处、工作/学习地点与时段、闲暇地点、生活圈、初次相识建议。57名新增居民使用固定种子档案；本轮不是随机生成人生系统。
- 关系网包括邻里、同事、供货、创作、活动、师生和父子关系。沈远的孩子沈晓有独立身份、日程和经历。关系与联系方式沿用原来的发现机制，不因配置存在就全部向玩家公开。
- 新人物进入既有城市作息和真实位置系统。普通聊天读到本人配置的话题，避免被通用NPC兜底台词覆盖。
- 统一聊天界面增加“听听你的经历”。按交情和每日限制逐步听取，人物资料只显示已经听过的内容。新人物传记与旧员工轶事分开记录，听一段不会误解锁另一套未听过的故事。
- 老存档保留已有人的住处、身份、已知关系与传记进度，初始化补齐新增人物；不重抽身份，不清空关系。原有供货、推广、物业、私人关系四条可玩人脉故事保持运行。

## 本轮完成的剧情配置

- 18个稳定ID的主线节点：三次跃迁、联合商户活动、两条稀有模型寻物线索、真实修复交付要求、上市条件模块、单行业结算月模块、花园试点与三种合作处理方式。
- 每个节点具有人物、地点、先行节点、出现条件、目标、核心对白、分支后果、推进凭据、结果和重试规则。
- 20名关键人物各有一条支线的起因、冲突与收束配置。普通居民的三段个人经历可以通过现有聊天听取；不把这360段经历说成360条可执行任务。
- 条件采用城郊5店＋现金100万元、社区6店＋暂定净资产3000万元＋自有品牌上市、单行业完整月营业收入1亿元＋对应发行人股价100元，行业事件次月触发。数值均保留为可调整配置。
- `CreatePlan(listingAtGarden, dominanceAfterGarden)` 支持移动上市模块及行业终章模块。节点ID保持不变；每次返回独立配置对象。

## 尚未完成的机制边界

这些新节点目前是内容配置，未接入完整的新主线执行器。没有通过查看档案、点击对话或作弊伪造“已经上市”“项目履约”“获得新街区资格”。自有品牌上市、对应发行人股价、真实模型交易修复任务、花园项目合同，以及新主线的凭据收集和奖励发放仍需正式接入；20条新支线也还不是完整可执行任务。

将来应分开保存事实、节点完成记录和已发放资格。调整条件所属阶段时保留事实和奖励记录，不重复发奖。当前还没有新主线进度存档，因此本轮不声称实现了该进度的迁移器。

人物的“工作地点”是日常活动位置，不代表自动增加建筑产权、可经营业态或无条件增加服务。人物知道谁也不代表玩家已拿到对方联系方式。

## 查看与维护

1. 正常游戏：在场所遇见人物，打开统一聊天，选择“听听你的经历”；通过“谈论别人”逐步了解关系。
2. Unity编辑器：作弊 → **人物与剧情配置（只读）**，分页查看全部人物、节点、支线和阶段条件。此入口包含剧透，仅编辑器可用，不改变存档。
3. Unity菜单：`PCShop/Export NPC and narrative catalogue` 从当前实际数据重新生成第47、48篇Markdown档案。
4. 数据源：`CityLife.NarrativePeople.cs`、`CityLife.NarrativeExisting.cs`、`DistrictNarrativeCatalog.cs`。新增人物仍应通过内容完整性与关系引用检查。

## 验证

验证在独立镜像 `D:\UnityProjects\PCShop\_ci_city_life` 执行，不关闭当前工作中的Unity编辑器。

- Unity编辑器脚本编译通过；档案导出命令成功执行。
- 最终8项 `NarrativeContentTests` 全部通过：身份与资料完整性、360段经历不重复、关系引用和无孤立人物、节点引用与前置顺序、20名关键人物支线覆盖、条件模块移动、读档保留经历、旧档补齐人口、查看不解锁信息、传记不误解锁旧员工轶事。部分断言合并在同一测试中。
- 相关回归共53项：45通过、8失败。失败案例使用 `bdc07c0913754b6ae0d14bebd05a121ff7f268e1` 中修改前的人物目录与会话逻辑在同一镜像复跑，8项均再次失败，失败信息逐项完全相同。没有把这些既有失败计为本轮通过。
- 对照时只修改隔离镜像，之后恢复本轮人物目录与会话文件；不回退共享工作区其他改动。
- XML记录：镜像内 `narrative-regression.xml`、`narrative-baseline.xml`、`narrative-final.xml`。该检查不等于已完成新主线实机通关；本轮不包含Windows构建。

既有失败清单：

| 测试 | 原有失败表现 |
|---|---|
| `CityActivityTests.FutureAppointmentWithEmptyJsonSeatRouteStillWalksToTheVenueAfterLoad` | 到场时已打烊，活动取消 |
| `CityActivityTests.RealTravelMealAndReloadSettleEachStageOnce` | 到场时已打烊，活动取消 |
| `CityStoryTests.LongNpcTripUsesSameOrdinaryTransportForEstimateAndActualSavedRoute` | 测试期待Taxi，当前路线选择Walk |
| `SocialTests.Appointment_BecomesRealTravelAndOnlyCompletedOutingGrantsRelation` | 到场时已打烊，活动取消 |
| `SocialTests.EnteringOwnersStall_OnlyMakesYouFamiliar_ContactIsAskedForInPerson` | 联系方式要求交情30，与旧断言不符 |
| `SocialTests.MissedAppointmentRecordsNoShowWithoutFalseCompletionOrMealCharge` | 测试期待NoShow，当前仍为Gathering |
| `SocialTests.SaveLoad_KeepsSocialState` | 旧社交状态断言失败 |
| `SocialTests.SmallTalk_OncePerDay_GainsRelation_AndTakesTenMinutes` | 当前聊天不推进10分钟，与旧断言不符 |
