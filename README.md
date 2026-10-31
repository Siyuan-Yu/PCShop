# PCShopSell（工作名）

电脑城创业故事：从商业街最偏的一间小电脑店起家，手装机赚第一桶金，靠行情、人脉和签约主播做大，最后建成自己的电脑乐园。3D 低多边形（固定斜俯视相机）模拟经营游戏（世界层 + 店内层 + 面板层 + 手操层），目标平台 Steam。

## 文档

- [`docs/25-product-weight-labels.md`](docs/25-product-weight-labels.md)：所有商品名称下的实际千克重量、各购买渠道和背包的统一说明

- [`docs/24-live-price-preview-and-confirm.md`](docs/24-live-price-preview-and-confirm.md)：调价时实时预览购买意愿，点“确定”保存售价；556项回归通过

- [`docs/23-fast-food-menu.md`](docs/23-fast-food-menu.md)：快餐店六项堂食菜单、饱腹/营养/心情、营业与扣费校验，554项测试通过

- [`docs/22-price-tags-quantities-and-sleep.md`](docs/22-price-tags-quantities-and-sleep.md)：价签加减、长按购物数量、次日自动到岗、自选起床与8小时睡眠；547项回归，无新增截图
- [`docs/21-cart-default-backpack.md`](docs/21-cart-default-backpack.md)：购物默认先放背包，超重再叫车；背包栏在前
- [`docs/20-unified-clock-and-kilograms.md`](docs/20-unified-clock-and-kilograms.md)：室内/街上走停统一时钟、全游戏重量统一千克、旧档等量转换
- [`docs/19-fridge-tiers.md`](docs/19-fridge-tiers.md)：四档冰箱、¥1,299–7,999、按型号累加容量及旧档保留
- [`docs/18-player-work-and-weekly-pay.md`](docs/18-player-work-and-weekly-pay.md)：零库存打工收入、平均约 ¥150/天、各店白/晚/夜班、一周合同及存读档
- [`docs/17-food-fridge-and-diet.md`](docs/17-food-fridge-and-diet.md)：家店通用冰箱、按菜名分开的熟饭、可乐购买/饮用、营养与心情记录
- [`docs/16-daily-multisource-purchasing.md`](docs/16-daily-multisource-purchasing.md)：每日多档口/多店整单采购、自动费用、完成按钮、追加招聘及最新验证
- [`docs/15-main-menu-and-languages.md`](docs/15-main-menu-and-languages.md)：最新初始菜单、中日英语言设置、存档选择及菜单验收范围
- [`docs/14-interaction-cleanup.md`](docs/14-interaction-cleanup.md)：交互简化、重复入口清理及验收；采购规则以第 16 篇为准
- [`docs/13-employees-and-procurement.md`](docs/13-employees-and-procurement.md)：员工无固定职业、自由安排岗位与门店、次日跨店、进货清单、车辆数量与占用、底栏叫货车
- [`docs/12-update-2026-10-31.md`](docs/12-update-2026-10-31.md)：最新工程快照、全部系统范围、物流/存储/多店/暂停规则、441 项测试及待修问题
- [`docs/11-update-2026-10-30.md`](docs/11-update-2026-10-30.md)：此前更新记录、当时的交互规则、截图及验证方法
- [`docs/10-current-content.md`](docs/10-current-content.md)：当前已实现内容总览
- [`docs/00-overview.md`](docs/00-overview.md)：概述与目录
- [`docs/02-design.md`](docs/02-design.md)：设计文档（定位、竞品与差异化、成长曲线、系统、垂直切片）
- [`docs/03-decisions.md`](docs/03-decisions.md)：决策记录（已定、已否决、默认假设、待办）
- [`docs/04-3c-and-world.md`](docs/04-3c-and-world.md)：3C、世界层与地产设计
- [`docs/05-player-life.md`](docs/05-player-life.md)：玩家角色、生活模拟、目标与失败
- [`docs/06-art-and-rendering.md`](docs/06-art-and-rendering.md)：美术路线、光影方案、可替换性约定
- [`docs/01-roadmap.md`](docs/01-roadmap.md)：开发路线
- [`docs/07-vs1-acceptance.md`](docs/07-vs1-acceptance.md)：VS1 验收说明（怎么玩、验收清单、数值位置）
- [`docs/08-current-status.md`](docs/08-current-status.md)：当前状态（已完成、代码结构、怎么跑测试、已知问题、下一步）
- [`docs/09-campaign-and-balance.md`](docs/09-campaign-and-balance.md)：三年长线目标、结局、店面扩建、固定人物与交情、数值校准
- [`docs/asset-log.md`](docs/asset-log.md)：AI 美术资源登记

## 技术
- Unity 2022.3.6f1，URP 14.0.8（3D 低多边形）；当前 UI 为 uGUI Text 与系统中文字体回退，TextMeshPro 中文字体资产仍待完成
- `Assets/Scripts/Core`：纯 C# 逻辑（无 UnityEngine 依赖）
- `Assets/Scripts/Game`：Unity 层
- `Assets/Tests/EditMode`：单元测试

运行测试（命令行）：

```
Unity.exe -batchmode -nographics -projectPath <项目路径> -runTests -testPlatform EditMode -testResults <结果.xml>
```

编辑器开着工程时，用 `tools/run-tests.ps1`、`tools/run-smoke.ps1`（在旁边的镜像目录里跑，见 `docs/08-current-status.md`）。

## 状态
购物车默认优先装背包，放不下的再叫车；左侧背包、右侧叫车，保留手动分配。当前 EditMode **540/540** 通过，见第 21 篇。

实时计时已统一：室内、街上移动和停下均使用同一真实时间与倍速，自动前往更新剩余时间。重量显示与计算全部采用千克，米价/配方/旧库存和在途配送按等量转换。时钟/重量版本 EditMode **537/537** 通过，本轮未生成截图，见第 20 篇。

冰箱现有单门、家用双门、对开门、四门四档，容量 60/120/240/360 千克、售价 ¥1,299/2,799/5,299/7,999。窗口统一显示千克，混放按实际型号累加容量；旧冰箱和食物保留。冰箱版本 EditMode **527/527** 通过，见第 19 篇。

店铺打工已加入：在营业店铺窗口签一周合同，按店设置日薪 ¥130–185（平均约 ¥150）、白班/晚班/通宵班、迟到扣薪和不同健康/心情消耗；到店自动工作，期间只保留倍速，整周领取工资。夜班支持打烊后凭合同进店、跨午夜及白天补觉。打工版本 EditMode **520/520** 通过，详见第 18 篇。

新增双门冰箱，家具城可购买并送到家或指定门店，摆放后可冷藏熟饭、食材和饮料。背包按菜名分叠并可选择吃哪种饭；可乐可购买后自己喝。营养和心情先只记录 0–100 的变化，不影响其他玩法。食物版本 EditMode **497/497** 通过，食物专项验证见第 17 篇。

初始主菜单已提供开始、加载、设置、帮助、关于、退出；菜单支持简体中文、日语和英语并记住选择。当前三语仅覆盖菜单与开场说明，经营界面尚未全面翻译。员工采购支持多档口、多商店混选；浏览货源只筛选目录，完整已选清单保留。点“完成”启用每日整单采购，费用自动计算，无需填写预算。招聘可保留候选人并再次贴告示，离店按钮已精简。

打开 `Assets/Scenes/Main.unity` 点 Play。当前包含单店经营流程、多家可进入的自营电脑店，以及直供账期、企业批单、委托门店、园区、创作者/MCN、私人关系、车辆与住房、物业及指数投资的首版规则。长期内容量与三年平衡仍需深化。

本轮已加入按阶段派发的当前任务、人物步行动作、暖色微缩街区美术、房产中介三维鸟瞰选址，以及保留家具原材质的绿/红发光轮廓。任务引导只说明操作，不提供一键前往；只有街道步行减速 50%，室内保持原速。2026-10-31 新增与调整包括多店配送、购物车、纸箱和三天临时存放、家具跨房间摆放，以及全部店铺侧栏统一服从暂停/继续、暂停时角色不能走动。随后新增统一员工管理、自由安排岗位与门店、次日跨店、员工采购、多人独占车辆与底栏“叫货车”。员工无固定职业，不擅长只影响效率，同店可即时换岗。此前多源采购 EditMode 485/485 通过；采购与交互专项图形验收记录见第 16 篇。旧完整冒烟在修改前与修改后均出现相同16项失败，详见交互清理文档。最新规则与待修问题见[本轮更新记录](docs/12-update-2026-10-31.md)，历史截图见[此前更新](docs/11-update-2026-10-30.md)，试玩步骤见[验收说明](docs/07-vs1-acceptance.md)。

冒烟测试（需图形，不能加 `-nographics`）：

```
Unity.exe -batchmode -projectPath <项目路径> -executeMethod PCShopSell.EditorTools.GameSceneBuilder.SmokeFromBatch -logFile <日志>
```

专项验收同样可通过镜像运行：

```powershell
./tools/run-smoke.ps1 -Method PCShopSell.EditorTools.GameSceneBuilder.AgencyFromBatch
./tools/run-smoke.ps1 -Method PCShopSell.EditorTools.GameSceneBuilder.PlacementFromBatch
```

同一镜像内的测试和冒烟应串行运行。镜像、缓存及测试存档保留在本地，不提交到仓库。
