# 青湾旧物市场验收

> 同日界面简化后：新增师傅一键修理，五个页签和一句话商品介绍；最新定向14/14通过，见`108-old-market-simple.xml`。真实界面按钮购买→自动切到“我买的”→点击付费修好→自动收起菜单→十五分钟后返还的图形流程由同一运行入口检查，见`108-old-market-simple-graphics.log`。下面24项及136项是简化前的兼容性记录，本次未重跑所有旧失败项。

2026-11-13，Unity 2022.3.6f1，独立`F:/_ci`镜像及测试存档；未重打Windows／WebGL包。

## 当前实现

新增独立市场，不改玩家二手店。新市场使用自己的12种旧物目录、每日库存、检测、修理台与回收价；商品ID可被背包读取，但不加入普通网购、零售供应和物价指数。71、72到新市场；原`old_market`活动及其主线寻物流程保留，另增`oldmarket_visit`作为普通新市场邀约。

## 检查结果

| 运行 | 结果 | 说明 |
| --- | --- | --- |
| 新市场定向 | 11/11通过 | 独立地点及路程、营业时间、现金／承重／到场、背包实物、每日库存及存档、三种正确修法、错误重试、离场暂停与维修存档、防重复回收、两项实际邀约及完成后见识奖励。当前11项也包含在下面24项复跑中。 |
| 最终兼容复跑 | 24项，23通过，1失败 | 新市场11项通过；原二手店收购／鉴定／上架容量／唯一商品结账等10项通过；两项主线寻物及解锁流程通过。唯一失败为旧二手店界面`UsedStock_`节点检查，下述对照同样失败。 |
| 初次扩展 | 136项，130通过，6失败 | 涵盖市场、零售、地区、前批展会、户外、真实成交和事件脚本。最初把普通`old_market`搬到新市场导致两项主线失败，已恢复原活动并另增新市场邀约；两项在最终兼容复跑通过。其他4项列在下方。该136项XML不是最终全量复跑，不能标成全部通过。 |
| 无新增市场的数据对照 | 5项，5项同样失败 | 只在CI镜像临时移除新市场地点／道路节点、新普通市场邀约及新增商品全局目录，把71、72配置恢复成旧模板；正式工程不动，对照后恢复CI到当前实现。这是新增市场数据移除对照，并非完整历史提交回退。 |

XML：`108-old-market-compatibility.xml`、`108-old-market-regression.xml`、`108-old-market-baseline.xml`。最新兼容复跑验证了商品已不进入`Gifts.All`普通全局目录。

## 对照仍失败的五项

- `CityRetailTests.CompetitionExpiresAndDoesNotDirectlyModifyCash`：预期一条竞争记录，实际零条。
- `DistrictProgressionTests.InvitationWaitsThreeDaysSurvivesReloadAndNotifiesExactlyOnce`：到期邀请节点为空。
- `DistrictProgressionTests.VisibleHomesHaveReachableDoorsAndStableResidentAddresses`：`residence_s_oldhomes_21`无人分配；失败发生在住户存在断言。
- `EventScriptTests.Writer_RoundTrips_AndMatchesTheFileOnDisk`：当前事件JSON格式与编辑器标准排版不一致。
- `ShopCoreGameplayTests.UsedCounterMovesConfirmedItemsToIllustratedShelfAndShowsSoldReceipt`：旧二手店库存页缺少测试预期`UsedStock_`节点。

这些检查在移除新增市场数据后仍复现，本次没有修改对应经营规则或顺带修复这些问题。不宣称全项目通过。

## 实际运行

图形入口：`PCShopSell.EditorTools.GameSceneBuilder.OldMarketFromBatch`。生产世界和控制器加载独立市场，检查四个实物摊位，打开真实商品菜单；实际支付购买旧收音机，五分钟检测、选择正确处理、十分钟维修，最后校验背包恰好一件修复实物、没有多出待修品。

日志：`108-old-market-graphics.log`。画面与运行说明在`../previews/oldmarket/`：`01-market-four-stalls.png`、`02-real-market-catalog.png`、`03-real-repaired-item.png`和`runtime-results.txt`。场景使用当前低多边形占位道具；未逐项录制全部商品。

`tools/add-old-market.py`选空地时检查道路、已有建筑、住宅、湖岸与入口通道，市场加入同一城市路网，不复用玩家二手店位置。`git diff --check`通过；现有赛车源文件仍有Git换行提示。
