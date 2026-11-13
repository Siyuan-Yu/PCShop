# 61–70 验收记录

2026-11-13，Unity 2022.3.6f1，独立 `F:/_ci` 工程镜像与验收存档。

## 结果

扩展 EditMode 回归 **124项，123通过、1项已有失败**，见 `106-outdoor-market-regression.xml`。

| 套件 | 通过／总数 |
| --- | ---: |
| 本批 OutdoorBatchTests | 27／27 |
| FishingTests | 40／40 |
| SocialEventBatchTests | 25／25 |
| CityProcurementTests | 9／9 |
| VenueBusinessTests | 8／8 |
| 事件配置校验 | 1／1 |
| CityActivityTests | 13／14 |

唯一失败为 `CityActivityTests.AllAuthoredWorkersRespectClosedVenuesOnInitializationAndDailyTicks`：`racer_0` 在关闭的 `race_0` 工作。此项已有基线和此前钓鱼回归记录，本次没有调整该赛车NPC的工作逻辑。不能把本轮描述为全项目测试全部通过。

## 本批覆盖

- 10个事件均通过真实预约、存读档、到场开始和实际扣款检查；不在答应邀请时发消费、交情或见识。
- AA、双人请客、私享野餐和私厨项目按确认金额收费。
- 70分别拒绝无店、其他店型、未装修的电脑店；预约后失去电脑店，到场会阻断。显卡核对需五分钟，切换型号后必须重新核对。
- 步道需要逐节点实际行走，半途读档继续同一路线；未走完不发完成奖励，完成后不能重复发奖励。
- 植物需到场逐株辨认，答案不同，答错仍能继续，奖励按正确数在结束时结算。
- 岗亭送餐扣背包中的真实主食，缺食物时可补齐后重试，工作地点不变；不把饮料当正餐。
- 行情来源使用真实市场、网购和已签协议直供，未签协议不编造第三份报价；真实线上与直供下单分别落到现有系统。
- 谈价和谈账期的低属性分支仍可选择，按0%失败，不能反复点击重掷。
- 显卡现款采购扣实价并生成真实货运；账期采购支付30%首款、记录70%真实负债，存读档后仍存在，次日把实际数量送入目的店库存。重复点击不生成第二单。

## 图形验收

入口 `PCShopSell.EditorTools.GameSceneBuilder.OutdoorBatchFromBatch`。生产GameController、活动详情界面、实际预约与到场、五分钟型号核对、现款采购和实际货运，独立存档。没有把商品直接写入库存来伪造采购结果。

图形运行 `SMOKE RESULT code=0 errors=0`。人工检查了实际1920×1080渲染的活动详情及采购界面，动作位于采购表单之前，可选择型号、数量、核对与账期。实际购买3张 `gpu1`，检查到真实货运条目。

- 日志：`106-outdoor-market-graphics.log`
- 截图：[到场](../previews/outdoor61-70/70-01-actual-gpu-visit.png)、[核对和采购](../previews/outdoor61-70/70-02-tested-model-and-order.png)、[实际货运](../previews/outdoor61-70/70-03-real-purchase-and-delivery.png)
- 运行计数：`../previews/outdoor61-70/runtime-results.txt`

未重打Windows／WebGL包，未逐项录制61–69的画面；植物线索和显卡型号核对仍使用现有场景、文字及商品数据，没有新增独立植物美术或显卡性能小游戏。51–58仍为待补做项。
