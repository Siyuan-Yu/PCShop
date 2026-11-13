# 31–40 赛车事件验证记录

日期：2026-11-13。Unity 2022.3.6f1，EditMode，使用 `F:/_ci` 工程镜像；主工程保留当前编辑器会话。Core、Game、Editor 和测试程序集编译通过。

## 最终结果

| 套件 | 总数 | 通过 | 失败 |
| --- | ---: | ---: | ---: |
| RacingEventBatchTests（本批新增） | 16 | 16 | 0 |
| EventScriptTests（所选定向项） | 16 | 16 | 0 |
| CityActivityTests | 14 | 13 | 1 |
| RacingTests | 31 | 27 | 4 |
| VenueBusinessTests | 8 | 8 | 0 |
| 合计 | 85 | 80 | 5 |

[最终 NUnit XML](101-racing-event-regression.xml) · [修改前基线 XML](101-racing-event-baseline.xml)

新增检查包括：

- 31 两种实际胜负、一圈和一位对手、不发现金奖、不增正式胜场、预约与实际夜宵扣款、预约存档和取消。
- 32 实际车辆电池老化、性能变化、存档、不能远程修复、车间收费和真实 30 分钟、途中断电退赛。
- 33 实际历史对手和正式预约；34 签约不预付、合同持久化、正赛付 ¥3,000、练习不付。
- 35 真实医院、陪诊到场后收费、完成且不扣玩家健康；36 解锁前可约、正式比赛时段、完成才认识人物及加见识。
- 37 单独捐 ¥500、不自动报名，到场报名 ¥2,000、完赛给奖牌而无奖金；38／40 真实装备、¥2,500 头盔、无即时治疗、装备和整修、同一碰撞的实际伤害降低与车损保持。
- 39 晚间时段、取消、场内路线移动、路径存读档及完成；31–40 整批编号和专用验收预置。

## 既有失败的基线核对

在镜像中将已修改的源码与配置还原为提交 `5880e9a`，移除本批新增文件后，对以下五项单独复跑，**同样五项失败**；主工作区没有回退。随后镜像重新同步本批实现，得到上方最终结果。

| 测试 | 修改前与修改后的现象 |
| --- | --- |
| CityActivityTests.AllAuthoredWorkersRespectClosedVenuesOnInitializationAndDailyTicks | 关闭的 race_0 中 racer_0 仍被标记在工作 |
| RacingTests.AcceptanceFixtureHasRealVehiclesShowroomAndIndependentSave | 测试预期 6 辆，验收档已有 9 辆 |
| RacingTests.PersonalVehiclesMoveBetweenGarageAndSizedShowroomWithoutDuplicates | 既有测试发生下标越界 |
| RacingTests.RivalsFinishingFirstRemainAheadAfterPlayerCrossesFinish | 预期第 3 名，实际断言得到第 11 名 |
| RacingTests.UsedCarLoopUsesContactsRealStockCashAndSingleDelivery | 测试反射调用参数与当前签名不符 |

本批遇到的 JSON 格式往返失败已通过外部事件编辑器标准格式重写解决，最终对应检查通过。目录仍为 123 个随机事件，41 项 C# 注册；编号 102 空号与 125–126 独立流程保留。

## 范围

- 这是无图形逻辑与程序集编译验证，不替代驾驶体验、镜头、贴纸、护具配色和夜骑模型的逐项画面验收。
- EventScriptTests 中原有的全选项、140 天频次和早晚期长模拟没有纳入本次最终套件，旧交接中的其他失败记录仍保留。
- 未重新打 Windows／WebGL 包，未变更本机个人存档，未提交或推送本轮工作区修改。
