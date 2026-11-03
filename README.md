# PCShopSell（工作名）

当前统一归档（2026-11-03）：[第51篇：现有内容与美术分支](docs/51-current-state-and-art-branches-2026-11-03.md)。main保存当前工业风美术、人物内容与最新UI整理；`art/industrial-reference`保存工业参考快照；`art/pixel-town`保存独立像素风方案。文档包含实际游戏截图、已实现/仅配置的范围与验证边界。本次源码联合专项47/47通过，未重新打包。

UI整理已落实：[第50篇：保留员工部分的UI整理](docs/50-ui-cleanup-excluding-employees.md)。统一经营、行情、人物和日程入口，拆分特殊店型页签，修正分店采购、资产跳转和财务范围，压缩常驻说明；员工部分保留。原审查见[第47篇](docs/47-ui-interaction-audit.md)。尚未重新打包。

最新人物内容交付：[第49篇：人物与主线信息配置接入](docs/49-narrative-content-implementation.md)，包含120名正式NPC、18个主线节点和20条关键人物支线配置。人物聊天与作息已接入，新主线执行机制仍待接入。此前开发快照见[第46篇](docs/46-snapshot-and-story-plan-2026-11-03.md)。未重新打包。

最新源码补充：[同类店铺商圈分流](docs/45-local-shop-competition.md)，半径40米内同类自营店含本店共N家时，客流保留1/N；中介地图和店铺商圈窗口显示范围圈、同类数量及分流后预计人数。尚未重新打包。

最新源码补充：[货架件数与承重双上限](docs/44-shelf-item-capacity.md)，普通20件/60千克、加固30件/100千克、重型40件/160千克；超出留后仓，库存和货架详情同步显示双上限。尚未重新打包。

最新源码补充：[中介每日经过人数与租金](docs/43-agency-daily-footfall.md)，用直观人数替代客流指数，综合地铁、住宅和面积，按人数统一报价并接入经营；已签租约保价。尚未重新打包。

最新源码补充：[最高售价倍率3倍](docs/42-markup-up-to-three.md)，覆盖此前1.6倍的店内、单品、居家工作台和餐馆菜单入口；基础购买意愿可为0，销售员和道具加成继续生效。尚未重新打包。

最新源码补充：[餐馆饮料加购](docs/41-restaurant-drinks.md)，购买意愿+20个百分点，真实陈列、扣货、收入和调价预览已接通，相关回归65/65通过。以下223752发行包尚不包含此补充。

当前完整归档：[第40篇：湖城、店型、采购、居家工作台与性能](docs/40-current-snapshot-2026-11-02.md)。包含已实现功能、已确认但未实施的剧情设计、测试边界与发行包信息；旧篇保留历史状态。

最新Windows x64包：`Windows-Optimized-20261102-223752`，包含树林分块、柜台增量刷新和居民运算优化。相关回归95/95通过，性能对照和验证边界见 [第39篇](docs/39-performance-optimization.md)，包信息见 [验证记录](docs/validation/39-performance-results.txt)。发行包不入库。

供货安排已细化为“批发商＋具体型号＋星期”的共享分配，支持每天／一三五／仅周三，详见 [第38篇](docs/38-supplier-model-allocation-and-weekdays.md)。

店型核心与供货已更新：科技迭代、模型热度、服装季节、二手风险收购、餐馆烹饪，以及线下每日供货与最大采购，见 [第36篇](docs/36-shop-core-gameplay-and-supplier-stock.md)。宠物店本轮不做。

冰箱保存规则已更新：冰箱内食物永久保存，取出后继续计算剩余保质期，详见 [冰箱永久保存](docs/35-permanent-fridge-storage.md)。

叙事方向已确认：架空近当代城市中的多业态经营与人物探索，通过三次引荐与真实合作，从城郊进入社区、公园与花园经营圈。统一依据见 [剧情总纲与四区晋升](docs/story-and-district-progression.md)；这是设计决定，实施状态以工程核对为准。旧“电脑城创业／电脑乐园终局”说明保留为历史设计与电脑业态内容，不再作为全店型总主线。游戏采用3D低多边形的世界层、店内层、面板层与手操层，目标平台Steam。

## 文档

- [`docs/47-city-npc-dossiers.md`](docs/47-city-npc-dossiers.md)：120名正式NPC的身份、住处、工作时段、三段经历及人物关系，从工程配置导出。
- [`docs/48-district-narrative-nodes.md`](docs/48-district-narrative-nodes.md)：18个成长主线节点、20条人物支线，以及可移动的上市／行业登顶条件模块；明确区分内容配置与任务执行。
- [`docs/49-narrative-content-implementation.md`](docs/49-narrative-content-implementation.md)：本轮接入范围、检查入口、存档兼容和测试结果。

- [`docs/story-and-district-progression.md`](docs/story-and-district-progression.md)：已确认叙事设计的统一入口；多业态定位、规则主题、四区成长、三次晋升、人物长期支线、可选投资再起，以及现有实现差异和待定事项。不是剧情实现或验收报告。

- [`docs/37-home-workbench.md`](docs/37-home-workbench.md)：家庭经营大屏、四页居家工作台、真实远程经营/投资/联系人、报表与品类行情，以及生活托管和存档；住宅扩容与实体安装启用留待后续。

- [`docs/34-shop-type-gameplay-ideas.md`](docs/34-shop-type-gameplay-ideas.md)：店型差异化设计；已确认简易餐馆、二手风险收购与买后鉴定方向，宠物用品保留但玩法后议，生鲜及花店退出玩家经营；已实施部分以第36篇为准。

- [`docs/40-current-snapshot-2026-11-02.md`](docs/40-current-snapshot-2026-11-02.md)：当前完整归档，统合第35—39篇与湖城、工作台、剧情设计及全部验证边界。

- [`docs/33-update-2026-11-02-gameplay-and-warehouses.md`](docs/33-update-2026-11-02-gameplay-and-warehouses.md)：历史归档；四片区中介与城市模型、实际商品/混售、场馆经营、同岗多人和规模上限、进货员采购历史、15秒现场结账耐心、纸箱B/F共享不限容量后仓，以及当时126项针对性回归与Windows包版本边界。

- [`docs/32-update-2026-11-02-city-v7.md`](docs/32-update-2026-11-02-city-v7.md)：上一完整归档；V7十街、八站、四档住宅与商业、大小混搭及186栋住宅填充，销售员/镜子加成、手机聊天、卸货与快捷交互。

- [`docs/31-update-2026-11-02.md`](docs/31-update-2026-11-02.md)：历史归档；城市生活与人物活动、统一购买/服务界面、通用陈列、店铺主售、实时数量、简化衣柜及天气换装，含当时存档/验证边界和源码清单。

- [`docs/30-update-2026-11-01.md`](docs/30-update-2026-11-01.md)：历史快照；多业态自营店、采购容错、账本/贷款/物价周报、日结工资、旧7天冷藏、装机/中介/人员显示及 WebGL 资源

- [`docs/29-food-expiry-badges.md`](docs/29-food-expiry-badges.md)：食物格子右下角倒计时、不同批次分格、临期提醒，574项回归通过

- [`docs/28-procurement-frequency.md`](docs/28-procurement-frequency.md)：历史每天/每三天采购规则；当前已改为第38篇的周历安排

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
- [`docs/12-update-2026-10-31.md`](docs/12-update-2026-10-31.md)：历史工程快照、当时系统范围、物流/存储/多店/暂停规则、441 项测试及待修问题
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

最新性能相关 EditMode 回归95/95通过，覆盖范围和报告见第39、40篇；较大历史回归有已记录的基线失败，不代表全仓库通过。测试在隔离镜像执行，不修改正式存档。

## 状态（2026-11-02）

当前源码包含湖城、电脑/服装/模型/二手/餐馆五类新开自营店、员工排班与多店共享限额采购、统一账务、人物关系与活动、家居饮食/睡眠、永久冷藏、居家经营工作台、生活托管、气候穿衣、资产投资和存读档。花店与生鲜不再供新开，宠物玩法暂缓。

买后带走的实体店共用“在场人物 + 商品目录 + 数量 + 购物车”窗口；当地消费的场所共用“在场人物 + 菜单/服务”窗口。普通零售允许实际陈列的混售，餐馆/二手使用专用循环；主售在柜台更改。库存入口提供本店进货员和每次采购记录。家店摆有纸箱时，B/F打开上背包、下不限容量后仓，多个箱子共用一个后仓。

衣柜保留手动试穿/换装和一键天气换装；生活托管可按设置自动更换已有衣物。具名NPC可交谈，匿名行人/顾客补充城市人流；交谈暂停，邀约写日程并可一键赴约，约定前后1小时到场可集合，单人/同行活动均有结算。

当前湖城V13含四片区中介、90个实体地点、1046个道路节点、84栋填充住宅；地图检查和实际图形专项通过。最新Windows包为20261102-223752，包含纸箱交互、店型核心、采购共享额度、工作台和性能优化。构建包按规则不入源码库，包指纹与验证资料已归档。完整内容、兼容及验证限制见 [最新归档](docs/40-current-snapshot-2026-11-02.md) 和 [现有内容总览](docs/10-current-content.md)。
