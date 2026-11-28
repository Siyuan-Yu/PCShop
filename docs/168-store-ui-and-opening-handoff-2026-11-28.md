# 当前交接 · 封面、商店经营与开店教学

2026-11-28，交付分支 `art/experiment`，Unity 2022.3.6f1。本文接续上一版，汇总本轮源码、界面实拍、营业额口径和远端新定稿。公司接管、全屏工作台、感情线、地图和三维动作等既有功能可从全量索引查找。[上一版玩法索引、动作与 Windows 交接](167-ui-animation-and-windows-2026-11-28.md)

## 封面、商店与账目

| 内容 | 已接入的行为 | 复拍与入口 |
| --- | --- | --- |
| 开始界面 | 复核湖城封面的长宽比、左侧遮阳棚、标题与副标题比例、暖米色按钮的棕边和投影。最近可读存档仍显示日期、现金和店铺数。有可继续的进度时首项为「继续游戏」，首次打开首项为「新游戏」。正式透明字标待入库，目前使用 PETSHOP 文字。 | [设计稿、上一版与本轮复拍](art-direction/implemented/menu-20261128/index.html) · [开始界面源码](../Assets/Scripts/Game/UI/GameUi.MainMenu.cs) |
| 商店可用现金 | 商店窗口标题栏右侧、暂停提示左侧显示现金图标和琥珀金额，与街道 HUD 共用个人余额。金额有变化时刷新。适用于批发市场、家具城、零售、现场餐饮购买、旧物市集、家具回收、车辆交易和整备、宠物用品购买窗口，以及手机网购和外卖。窗口内原有重复余额已移至标题栏。 | [实体批发](art-direction/implemented/store-opening-20261128/cash-wholesale.png) · [车行](art-direction/implemented/store-opening-20261128/cash-dealership.png) · [手机网购](art-direction/implemented/store-opening-20261128/cash-online.png) · [共用窗口标题栏](../Assets/Scripts/Game/UI/GameUi.cs) |
| 结账和收款按钮 | 玩家店内柜台有顾客可结账时，按钮优先用琥珀主操作样式。二手店结账、餐馆结账、交付收款及模型委托收款沿用同一优先规则。教学聚光中保留按钮本身的琥珀底色。没有可结账项目时，禁用按钮维持灰色。采购窗口的购物车已有对应的结账样式。 | [首单教学实拍](art-direction/implemented/store-opening-20261128/checkout.png) · [柜台操作](../Assets/Scripts/Game/UI/GameUi.Shop.cs) |
| 宠物寄养营业额 | 「今日经营」营业额、门店收入明细、经营趋势与关店日报计入本店实际收到的寄养款。首周预付款在接单当天入账，首周余款扣除预付款，续周按实际收款日入账。仅有寄养收入的当天也生成日报。旧档中可根据门店和订单来源识别当前租期的已收款，加载时补营业额归属，保留现金余额与原流水。 | [寄养收款](../Assets/Scripts/Core/Session/GameSession.PetBoarding.cs) · [账目与旧档归属](../Assets/Scripts/Core/Session/GameSession.CityFinance.cs) |

## 开店教学与员工

- 首次去中介、开店采购、回店装修或回家等步骤，教学优先指向底栏适用的快捷入口。家具城这类临时目的地可借用「选择目的地」，步骤完成后保留玩家原有设置。普通追踪任务有相应底栏入口时用浅琥珀提示，仍可正常游玩。到场后的操作或缺少可用快捷入口时，任务卡保留操作入口。[去中介](art-direction/implemented/store-opening-20261128/shortcut-agency.png) · [去批发](art-direction/implemented/store-opening-20261128/shortcut-wholesale.png) · [底栏与任务入口](../Assets/Scripts/Game/UI/GameUi.CityNavigation.cs)
- 招聘告示贴出后，开店教学首次进入打烊回家步骤时暂停时间，高亮底栏住处卡。玩家点卡后按正常路线回家，抵达后仍由玩家决定何时睡。引导已完成的状态随存档保存。[打烊回家](art-direction/implemented/store-opening-20261128/shortcut-home.png) · [教学阶段判定](../Assets/Scripts/Core/Session/Quests.Opening.cs)
- 「员工 → 开店分工」的列表每个适用岗位显示一行岗位名和状态。标题旁的问号可随时打开完整岗位说明，教学到这里高亮同一个问号。[门店员工页](../Assets/Scripts/Game/UI/GameUi.Shop.cs)
- 「聘用与工作安排」保留全部 11 个岗位选项，教学只高亮当前门店适用且尚缺的岗位。宠物用品店会提示收银员、进货员和饲养员。其他岗位可点开查看能力和限制，确认聘用时仍按原有店型规则判断。[完整岗位与高亮](art-direction/implemented/store-opening-20261128/hiring-roles.png) · [其他岗位预览](art-direction/implemented/store-opening-20261128/hiring-ineligible.png)
- 选中岗位后并排显示「岗位看重」与「预计上岗效果」。左边显示本人的相关能力数值和用途，右边显示按现有公式算出的工作用时、进货折扣、顾客购买意愿或服务效果。招聘及在职改岗共用。截图中的许明智力 86，选择进货员时预计进货折扣 8.6%。[进货员能力与效果](art-direction/implemented/store-opening-20261128/hiring-buyer.png) · [岗位选择与预览源码](../Assets/Scripts/Game/UI/GameUi.Interactions.cs)

## 实拍、验证与换机

- 本轮已有的 Unity 界面截图包括 1920×1080 与 1280×720 的封面、有档及无档状态，以及商店现金、首单结账、底栏教学和岗位选择。首单教学截图确认可点的结账按钮为 `#B8812A`。这些画面使用隔离样本档复拍。完整开店流程和寄养跨周、旧档账目仍待玩法复测。
- 本轮 Git 同步没有新增 Unity 测试或 Windows 构建。16:41 的 Windows 包对应上一版源码。本轮截图和设计稿存放于 `docs/`，无需导入 Unity。[上一版 Windows 包与校验](167-ui-animation-and-windows-2026-11-28.md#windows-x64-完整版)
- 换机后拉取 `origin/art/experiment`，沿用 Unity 2022.3.6f1。封面可用 `PCShopSell.EditorTools.GameSceneBuilder.MenuCoverFromBatch` 复拍，输出目录用 `PCSHOP_UI_ALIGN_CAPTURE` 指定。[批量复拍入口](../Assets/Editor/GameSceneBuilder.UiAlignCapture.cs)

## 远端新增定稿

- 美术规范 §21 增加暖炭底、淡暗角与柔和落地阴影的室内背景标准，覆盖住宅、门店和写字楼。本轮同步设计稿和规则，室内效果在后续实现并按四区复拍。[室内背景规范](art-direction/STYLE_GUIDE.md#21-室内背景统一为暖炭底2026-11-28-用户确认)
- 美术规范 §22 增加地点门牌编码、10 类 × 4 级图标与底栏两行卡片。采用「方案甲」，起始住处显示 `01街 27号`。卡片主体前往，右侧下拉换目的地。外观和地点数据待实现。改版时沿用快捷入口的控件名和交互，接住本轮任务高亮及打烊暂停流程。[地点与底栏规范](art-direction/STYLE_GUIDE.md#22-地点编号体系等级图标与底栏快捷前往2026-11-28-用户确认)
