# PCShopSell（工作名）

电脑城创业故事：从商业街最偏的一间小电脑店起家，手装机赚第一桶金，靠行情、人脉和签约主播做大，最后建成自己的电脑乐园。3D 低多边形（固定斜俯视相机）模拟经营游戏（世界层 + 店内层 + 面板层 + 手操层），目标平台 Steam。

## 文档

- [`docs/30-update-2026-11-01.md`](docs/30-update-2026-11-01.md)：本次完整快照；多业态自营店、采购容错、账本/贷款/物价周报、日结工资、7天冷藏、装机/中介/人员显示及 WebGL 资源

- [`docs/29-food-expiry-badges.md`](docs/29-food-expiry-badges.md)：食物格子右下角倒计时、不同批次分格、临期提醒，574项回归通过

- [`docs/28-procurement-frequency.md`](docs/28-procurement-frequency.md)：持续采购可选每天或每三天，显示下次日期并保存周期

- [`docs/27-next-day-employee-procurement.md`](docs/27-next-day-employee-procurement.md)：采购只保存、次日起执行、按现金与清单顺序部分购买、余款退回，563项回归通过

- [`docs/26-fast-food-takeaway.md`](docs/26-fast-food-takeaway.md)：快餐买后入包、真实重量、手动与自动进餐、冷藏及存档，560项测试通过

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
- Unity 2022.3.6f1，URP 14.0.8（3D 低多边形）；当前 UI 为 uGUI Text；桌面使用系统中文字体，WebGL 使用内置 Noto Sans CJK SC，TextMeshPro 中文字体资产仍待完成
- `Assets/Scripts/Core`：纯 C# 逻辑（无 UnityEngine 依赖）
- `Assets/Scripts/Game`：Unity 层
- `Assets/Tests/EditMode`：单元测试

运行测试（命令行）：

```
Unity.exe -batchmode -nographics -projectPath <项目路径> -runTests -testPlatform EditMode -testResults <结果.xml>
```

上述命令与现有测试工具供另行授权的回归使用。本轮按玩家要求只做隔离镜像 Editor 编译，不运行单元测试、图形冒烟或游戏截图。历史测试成绩不代表当前版本已回归。

## 状态（2026-11-01）

开局经营电脑店，第 15 天起可在中介签新分店时选择花店、生鲜店、食品店。已接入批发货源、背包/纸箱上架、独立店内库存与员工、对应商品陈列和顾客收银，生鲜和外带食品按批次保存到期日。

采购员按每天/每三天的清单持续采购；单项失败继续后面的商品，额度、现金、载重和仓储不足时尽量部分购买，结果保留未购原因。电子市场工作日营业至 20:00，应聘者当天 18:00 离开。

顶部现金可打开总收支账本，按天/周/月看全部角色现金往来；「行情」可看物价周报并开关订阅；手机应用商店提供月息 20% 单利、最长 12 个月的「周转借款」，到期允许强扣为负。

打工保留一周合同，工资改为每班收工自动到账；冰箱首次冷藏额外延长 7 天，在当地可自动吃冷藏即食食品；外卖可选 ¥25/100/300/800 四档；底栏可一键回家走到床边选择睡眠时间。人脉增加实际相处历史，装机界面常驻订单配置预览，整机销售增加服务溢价。

中介地图保留建筑名、绿/橙可租高亮与右栏详情，提前交房等待时间加粗突出；所有店内改布局均需当天歇业装修。小电驴和三轮车都有游戏内禁行、罚款与扣押规则。

本轮只检查编译，未运行玩法、回归测试、图形冒烟或截图。内置 WebGL 字体、存档同步与材质引用已保存，但旧 WebGL ZIP 不含之后的全部功能，本次未重打包。当前具体规则、入口、存档兼容和待验收边界以 [完整快照](docs/30-update-2026-11-01.md) 为准。
