# 当前工程归档验证 2026年11月5日

## 联合检查

Unity 2022.3.6f1，当前Assets同步至隔离副本后，以无图形EditMode运行。没有打开正式玩家存档或新增截图。

检查组：RacingTests、KolStreamingTests、QuarterlyExpoTests、QuarterlyExpoSceneTests、ResidentialPropertyTests、ResidentialAgencyTests、AgencyDistrictViewTests、ExpoAcceptanceTests、NarrativeContentTests、McnIntroductionTests、BrandBusinessTests、BrandStoryTests、DistrictCommerceTests。

首次144项，143通过、1失败，耗时101.57秒。[原始报告](71-current-archive-tests.xml)。唯一失败为 `BrandStoryTests.FormalNamesAreConsistentAcrossCataloguesAndPreserveAllStableIds` 仍预期120名正式NPC，实际124；已更新为包含4位车友的124。姓名、旧ID和别名断言保留，没有更改人物生成规则来满足旧数字。

修正后的品牌故事复测见[71-brand-story-recheck.xml](71-brand-story-recheck.xml)，结果在下方记录，不把重叠检查重复计数。

复测16/16通过，耗时4.05秒。因此本次所选144项在首次检查与必要复测后均有通过记录；不是160项不同测试。仅修改过时的人数断言，其余代码保持联合检查时版本。

## 图形与验证边界

上一轮按用户要求截取真实Unity鸟瞰，检查草坪、四个场地和道路位置，并修正登岸缝隙，见[第70篇](../70-racing-actual-map-review.md)。渲染完成，原主路线1440分钟、初始住所校验通过。Unity许可客户端有启动握手警告，随后完成编译和输出，不能称为日志完全无警告。

赛车此前26项专项及相关联合报告见第69篇。本次不是全仓库1212项全量验收，也没有重测帧率、打Windows包或人工驾驶。旧员工系统3项失败见[既有记录](69-racing-results.txt)，未宣称本次修复。

## 归档范围

当前全部源码、地图、4张车友头像与Unity元数据、第60—71篇专题及总览、概念图、既有获准预览和测试报告纳入归档。已提交的主线与背包交付等内容仍在仓库，总览包含当前状态。缓存、玩家存档、临时工作文件和构建包按既有规则排除。
