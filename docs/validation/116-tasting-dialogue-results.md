# 自然试喝对话验证

2026-11-14，Unity 2022.3.6f1，隔离工程 `F:\_ci`。本次按用户要求把53试喝改为人物对话选饮品与自然味觉反馈。

`OnsitePlayTests` **33/33通过**。保留原29项现场事件验证，新增三种对话选择的实际调制／试饮、口味反馈、即时属性变化、最终结算不重复加智力，以及重复品尝／JSON读档的成长去重。结果见[116-tasting-targeted.xml](116-tasting-targeted.xml)。

关联回归 **100/100通过**：现场33、社交25、社区生活20、街坊21、事件数据格式1。结果见[116-tasting-regression.xml](116-tasting-regression.xml)。本次未修改其余事件的比分、照片数量或夜校答题规则。

实景入口 `GameSceneBuilder.TastingDialogueFromBatch` 使用正式GameController和UI，实际预约、到场、对话选饮品、调制、接杯试饮，先产生奇怪口味的健康／精力变化，再产生完美口味的即时心情／智力变化，并完整结算。扫描可见UI，确认不存在“已试喝”“正确”“错误”“目标：”“剩余材料”“/3杯”；结算不含成功次数或实际回合计数。

图形验收通过，`SMOKE RESULT code=0 errors=0`。截图与[运行记录](../previews/tasting-dialogue/runtime-results.txt)：[对话选择](../previews/tasting-dialogue/53-dialogue-choice.png)、[接杯](../previews/tasting-dialogue/53-drink-offer.png)、[奇怪的味道](../previews/tasting-dialogue/53-odd-taste.png)、[完美的味道](../previews/tasting-dialogue/53-perfect-taste.png)。新版对话、两种味觉反馈共三张图已通过飞书发送给用户。

上一轮跨模块224项223通过，保留一项在旧源码已复现的赛场员工闭店测试失败，见[115结果](115-onsite-results.md)和[114基线](114-neighborhood-results.md)。更早五项货运／装修失败见112验证。没有把这些历史问题计作本次全工程已通过；本轮不重新打包安装程序。
