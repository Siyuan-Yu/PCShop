# 当前交接 · 界面、三维动作和 Windows 完整版

2026-11-28，交付分支 `art/experiment`，工程版本 Unity 2022.3.6f1。本次构建使用 `daa6a454` 加当时工作树里的源码与资源改动。仓库提交后的工程内容可以作为这次构建的重建来源。

## 当前玩法入口

| 内容 | 目前的入口和规则 |
| --- | --- |
| 开局与城镇 | 从开场演出、出租屋、购手机、租铺和首单进入经营。主线依次开放城郊、社区、公园、花园，赛场从实际道路进入。[全量玩法与主线](153-current-gameplay-sponsorship-and-relationships-2026-11-23.md) · [街区进度](story-and-district-progression.md) |
| 门店与员工 | 门店柜台、库存、定价、订单、装修和员工页。岗位任命、招聘、解雇、排班和满意度接入实际门店经营。公司员工另在「经营 → 公司」管理，超编提示后由玩家决定。[员工规则](155-employee-wellbeing-2026-11-27.md) |
| 品牌与公司 | 登记品牌对应开公司。花园开放后可走上市、股东、董事会及双向控股流程。完成接管后，交接通知列出获得经营权的门店及个人门店，写字楼由「经营 → 公司」直达。大厅左侧前台可查看门店、任命负责人及核准招聘。[股权与交接](156-company-equity-and-control-2026-11-27.md) · [前台与招聘实机](165-current-handoff-and-resource-cleanup-2026-11-28.md#实测与验收边界) |
| 住宅、工作台和证券 | 房屋租购、摆放、储物、烹饪与维修在住处处理。全屏工作台有今日、门店、投资、财务、联系人五页，证券可由手机、银行及工作台打开。[证券与居家工作台](art-direction/证券与居家工作台实施记录.md) |
| 生活、感情线和人物 | 24 位可攻略人物包含 12 条专属故事和 12 位普通可攻略角色。按对象与关系状态提供独处、赴约对话、专属 CG、表情和交往后的回应。人物、猫狗、正式商品图标及场所玩法从全量交接进入。[感情线](relationship-stories.md) · [全量内容与美术索引](165-current-handoff-and-resource-cleanup-2026-11-28.md#当前玩法与入口) |

## 本轮界面与开店教学

- 按美术规范 §19 调整通用窗口、圆角卡片、按钮、页签、列表、进度条、街道 HUD 和底栏。落到手机九宫格、个人资料、店铺柜台、经营与财务、住处设施、交通和系统窗口。证券与全屏工作台沿用 §18 的内容布局，并补图片、字阶与共用样式。[美术规范](art-direction/STYLE_GUIDE.md) · [设计稿与 Unity 复拍](art-direction/implemented/ui-align-20261128/index.html)
- 任务卡可点击的主操作保持琥珀强调，家具城等购物车把结账作为主操作，配送选项采用浅琥珀底与描边。员工页「开店分工」每个适用岗位一行，显示岗位名及安排状态。教学高亮标题旁的问号，玩家点击打开原有岗位说明。教学后同一入口继续可用。[任务入口](../Assets/Scripts/Game/UI/GameUi.Quests.cs) · [岗位说明](../Assets/Scripts/Game/UI/GameUi.Shop.cs)
- 正式界面改读 `Assets/Resources/UiArt/Icons` 下的 67 张独立 256×256 透明 PNG，移除运行时的 6 张旧整页 JPG 及其导入文件。切图源稿归入美术文档，Unity 菜单「PCShop → 界面 → 导出单张图标」可重出单图。[切图入口](../Assets/Editor/UiIconSlicer.cs) · [图标接触表](previews/ui-icons/index.html)
- 开始界面采用 §20 定稿的湖城封面，显示最近可读存档的日期、现金和店铺数量；开始页「继续游戏」连接最新可读存档，已有运行会话从暂停菜单返回时继续本局。自动档和手动档有独立显示区域。独立正式 Logo 贴图尚未入库，目前用 `PETSHOP` 标题文字。[开始界面](../Assets/Scripts/Game/UI/GameUi.MainMenu.cs) · [存档界面](../Assets/Scripts/Game/UI/GameUi.SaveSlots.cs)
- 工作台日进度条首次打开时即显示当前游戏时间。之后按游戏分钟更新标记与文字。长通知卡为正文留出行高，带实景的截图仍使用游戏当前的实际字段。[工作台实现](../Assets/Scripts/Game/UI/GameUi.HomeWorkbench.cs)

## 三维动作、道路与 CG

| 范围 | 已接入 |
| --- | --- |
| 共用动作 | `FigureMotion` 通过现有骨架姿态播放交互。玩家、员工和场景人物共用动作，手部目标按柜台、货架、工作设施和宠物道具位置计算。 |
| 柜台、货架与入库 | 玩家或当班员工结账，顾客按支付方式付现或扫码，自助结账使用柜台终端。支持来客看货、拿取和放回，以及玩家入库或补货的纸箱动作。模型委托接单、交付收款与二手台收购、鉴定也有动作反馈。 |
| 店内工作与日常 | 寄养接送、喂食、换水和清理，二手修复及模型喷漆，现场会面和店员招呼、写字楼前台招呼、住宅工作台操作均接入现有动作触发。动作由周围实际有可见角色与交互对象时播放。 |
| 城郊道路 | 城郊道路范围沿片区关口从起始住宅沿路网计算。车道、人行道、入户接道和标线共用同一分区口径，针对农贸市场到五金铺附近的突兀色段。地图缓存版本升到 **18**，本次完整地图已重烘。 |
| 开场演出 | 已替换游戏使用的开场 CG 视频。Windows 内的文件和工作树原视频 SHA-256 一致。 |

[动作实现](../Assets/Scripts/Game/Runtime/FigureMotion.cs) · [门店动作触发](../Assets/Scripts/Game/Runtime/ShopInteriorView.Interactions.cs) · [道路材质](../Assets/Scripts/Game/Runtime/StreetWorldBuilder.RoadSurfaces.cs) · [开场视频](../Assets/StreamingAssets/Opening/OpeningCg.mp4)

## 验收与续接

- Unity 编译和界面复拍完成，主批次输出 50 张截图，截图过程记录 0 个错误。对照页覆盖 §19 的 39 张参考图、§18 证券和居家工作台，以及 §20 开始界面。另用已开放花园的隔离档取得证券真实买入和做空状态。图标切图工具重新导出后，67 张 PNG 与当前资源逐张一致。[界面对照说明](art-direction/implemented/ui-align-20261128/README.md)
- UI 的 95% 目标仍待整体验收。开始界面待正式 Logo，门店订单和部分经营列表仍有可见排版差距。个别设计稿含早期占位字段或旧住宅界面，比较时以实际经营数据和现行全屏工作台为准。
- 本轮未启动 Windows 客户端做游玩测试。道路和强调色的 EditMode 用例已更新，本轮未运行。三维动作接下来按正常游玩距离复核首单的玩家、员工与自助结账，并检查不同模型的侧背视角、手位和道具关系。
- 前一版的功能、性能和体验结果保留为当时版本的证据。长线感情体验、核准招聘后去面试门店的衔接、四区和赛场的 95% 美术验收仍见对应专题记录。[前版复测与性能](166-qa-and-windows-2026-11-28.md) · [三区与赛场换机交接](art-direction/四区场景复核/三区实机验收/换机交接.md)

## Windows x64 完整版

| 项目 | 记录 |
| --- | --- |
| 包名 | `PCShopSell-Windows-x64-20261128-164150-daa6a454.zip` |
| 本机目录 | `F:/PCShopSell_builds/PCShopSell-Windows-x64-20261128-164150-daa6a454/` |
| 构建来源 | `art/experiment` 的 `daa6a4543277c0002b6064154a6480ccd5c2955a` 加本篇源码与资产。已逐字节核对隔离目录中 199 个新增或修改的工程文件，12 个已移除图集文件及元数据在构建目录同样已移除。 |
| 构建 | Unity 2022.3.6f1，Windows x64 Mono，1920×1080 可调整窗口，完整地图格式 18，LZ4HC。地图烘焙保留 3,111 个共用网格、34,378 条树林部件摆放记录。 |
| 大小 | ZIP **326,852,539 字节**，约 **311.7 MiB**。ZIP 内 133 个文件。 |
| SHA-256 | `e3479ac9cec3904b392ba880b921a7956f152408286f15341c9b268bbf636d43` |
| 校验 | Unity 构建成功，已核对 PE 架构和运行依赖，逐项解读 ZIP 并比对源文件 SHA-256。完整解压后运行 `PCShopSell/PCShopSell.exe`。 |

GitHub 交接保存工程、文档和下列构建证据，ZIP 存于上表的本机目录。[构建与校验记录](validation/2026-11-28-ui-animation-windows/构建与校验.json)

换机后拉取 `origin/art/experiment`，用 Unity 2022.3.6f1 打开。复制 `Assets`、`Packages`、`ProjectSettings` 到独立构建目录后运行下列命令。生成的 `Assets/Generated/CityBake` 随源码和资源指纹重建。[地图预生成与加载](地图预生成与加载.md)

```powershell
& '<Unity 2022.3.6f1>/Editor/Unity.exe' -batchmode -projectPath '<独立构建目录>' -buildTarget StandaloneWindows64 -executeMethod WindowsPlayerBuild.Run -pcshop-build-output '<新的输出目录>/PCShopSell/PCShopSell.exe' -logFile '<日志目录>/unity-build.log'
```

复拍可用 `PCShopSell.EditorTools.GameSceneBuilder.UiAlignFromBatch`，通过环境变量 `PCSHOP_UI_ALIGN_CAPTURE` 指定输出目录。证券和工作台的真实持仓样本用 `PCShopSell.EditorTools.GameSceneBuilder.StockAndWorkbenchFromBatch`。[界面复拍入口](../Assets/Editor/GameSceneBuilder.UiAlignCapture.cs) · [证券与工作台复拍入口](../Assets/Editor/GameSceneBuilder.StockView.cs)
