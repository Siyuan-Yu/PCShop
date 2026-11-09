# 界面精简与买东西 / 采购 / 进货员界面改版（2026年11月9日）

本篇记录 11 月 9 日对界面的一轮集中整理：新手开局的做饭教程、居家经营工作台、顶栏与提示精简，以及所有“买东西”页面、采购页和进货员页的改版。[当日完整内容归档](93-current-game-content-2026-11-09.md)

## 1. 新手开局与居家经营工作台

- 开局强制做一次“开局做饭”教程：`GameUi.OpeningMeal.cs` 引导玩家完成备料到出锅，完成前不放行后续任务。编辑器场景 `GameSceneBuilder.CookingTutorial.cs` 可单独截图验证。
- 居家经营工作台按页面重做（`GameUi.HomeWorkbench.cs`、`GameUi.HomeWorkbenchKit.cs`、`GameUi.HomeWorkbenchBusiness.cs`）：账目、门店、库存、采购入口集中在“我的”里。`Ui.FlatSkin.cs` 提供深蓝扁平皮肤（`FlatBand`、`FlatRow`、`FlatCard`、`FlatHotEdge` 等颜色和 `FlatPanel`）。
- “资产”入口现在指向居家工作台；旧的 `Workspace_business` 面板不再出现。

## 2. 顶栏、提示与小件精简

- 顶栏：人物、员工、日程、品牌四个按钮收进“更多 ▾”弹出菜单（`GameUi.HudSlim.cs`），有模态窗口时自动收起。
- 标签条：`TabStrips.Restyle` 自动把窗口里“一行 3–6 个等宽按钮、只有一个高亮”的切换条改成固定宽度的页签样式，在 `Window.Rebuild()` 里统一生效。
- 提示：通用提示只在开局前两天出现，空提示不再显示底条；相同的浮动提示会刷新计时而不是叠加。
- 左上任务卡高度由 `textTop + 130` 收到 `+ 118`。
- 载具按钮：不可用且未持有的载具不再显示。
- 采购页的商品行高度由 88 收到 52，按钮 190×40。

## 3. 买东西：左商品、右购物车

所有走购物车的买东西页面统一成同一版式（`GameUi.CartSide.cs`）：

- 左边商品列表，每行两行小字（名称加每件重量、来源、说明），价格、数量加减、“最大”。选了的行只高亮，不再把列表撑长。
- 右边固定 520 宽的购物车侧栏：“装进背包”和“叫车送”两组各自带进度条、挪动按钮和移除按钮；送货地、“背得动的放背包 / 全部叫车”、合计和结账固定在底部。
- 窗口宽度统一为 `StoreWindowWidth = 1620`，带“在这里打工”行的窗口会按多出的行数压缩人物栏高度。

适用范围：电子批发市场（含别家档口）、家具城、服饰店、快餐、农贸市场、烟酒茶行、手机店、零售批发和展会采购（都经 `BuildCarryStore` → `BuildCarryCatalog`）；网购的“电脑配件”标签（`GameUi.Wholesale.cs`）。

网购其余五个标签（家具、店里摆设、衣服、礼品、大米）也改成同一版式（`GameUi.OnlineParcels.cs`）：左边挑东西，右边“待下单”侧栏，点“下单”一次买下所有已选。衣服每件只能买一件，已有或在路上的不能再选。

不改的买东西页面：汽车经销商和车辆整备（一辆一辆买，购买会弹出专门的购车窗口）。

修复：打开电子批发市场时，别家档口的商品键 `part:<配件>:<档口>` 在 `ShopQuantity` 里被当成配件编号，抛出“配件不存在”，已改为拆键取档口剩余量。

## 4. 采购页：两层结构

`GameUi.CityProcurement.cs` 负责入口，`GameUi.ProcurementShops.cs` 负责页面。

- 入口：`OpenCityProcurement()` 进入店铺总览（只有一家店时直接进该店）；`OpenCityProcurementFor(shop)` 直接进某家店；`OpenProcurementAgreement()` 打开直供协议页。
- 第一层选店：每家店一张卡，显示补货几种、缺货几种、在途几种，进货员和下次进货时间；“进入 ›”进第二层。
- 第二层补货清单：只列已设补货目标的商品，行内显示库存、在途，目标和下限用加减直接改，“负责”按钮在多名进货员之间轮换，“移除”清掉目标。缺货行标红。支持“全部 / 缺货 / 在途”筛选和搜索。
- “＋ 添加商品”弹出选择窗，默认目标 6、下限 2。新加补货默认交给本店第一位进货员，没有进货员时提示先去招聘。
- 进货员栏显示进货员和下次进货时间，“安排进货 ›”进入进货员页。在途订单可展开。直供协议和全店补货预算保留在顶部。
- 数据仍用原有的 `ProcurementPolicy`，只是编辑方式从弹窗改为行内。

## 5. 进货员页：左店家商品、右采购清单

`GameUi.Employees.cs` 的 `OpenEmployeePurchase` 重排为左右两栏（窗口 1560×880）：

- 左边：货源按钮、分类、搜索、“只看已选”，下面是商品列表，每行名称、货源、库存、在途、起订量，价格、数量加减和“最大”。当日上限和可分配额度改放鼠标悬停提示里。
- 右边（540 宽）：“采购了什么”已选清单（可调顺序、移出、清空）、当前状态、频率三个按钮、交通五个按钮、预算合计和“完成”，已启用时多一个“暂停采购”。
- 右上有“固定清单 / 按补货清单自动补”切换。选“自动补”时，左边显示说明和“去调整补货清单 ›”，右边是按目标 / 下限和当前库存算出的“下一趟”只读预览。
- 预览来自新增的公共接口 `EmployeePurchaseQuote.Preview`（`EmployeePurchaseQuoteLine`：名称、货源、件数、小计、重量），位于 `GameSession.Workforce.cs`。
- 没有做的：彻底去掉“固定数量清单”、让每家店只有一份补货清单。这会改存档数据逻辑，需要另行决定。

## 6. 验证与测试

- 编辑器截图场景：`GameSceneBuilder.UiAudit.cs`、`GameSceneBuilder.PurchaseAudit.cs`（`PurchaseAudit` 到 `PurchaseAudit4`，含网购各标签、采购、进货员）、`GameSceneBuilder.HomeWorkbenchView.cs`、`GameSceneBuilder.StockView.cs`。用 `tools/run-smoke.ps1 -Method PCShopSell.EditorTools.GameSceneBuilder.<名称>FromBatch` 在镜像工程里运行；生成的截图和 HTML 草图已删除，不入库。
- 更新的测试：`PurchasePlanTests`（进货员窗口布局改查新节点路径）、`UiCleanupTests`（采购页改用 `OpenCityProcurementFor`）、`HomeWorkbenchTests`、`QuestTests`。
- 本轮在镜像里跑的相关测试：采购与进货员相关（`PurchasePlanTests`、`SupplierPlanAllocationTests`、`WholesaleMinimumTests`）35 个中 3 个失败（`PartialCash…` 两个、`PostingAnotherAdRetains…`），改动前就已失败。
- 更大的一组（UI、购物车、展会、竞争、工作坊、`HomeTests`、`BusinessRegressionTests` 等）共 320 个，246 通过、74 失败。失败集中在店铺竞争、房租、展会、车辆收支、快递、装修等逻辑，以及 `UiCleanupTests` 里查找旧资产 / 日程 / 房产面板的 6 个；没有做改动前的逐项对比，不能保证全部与本轮无关。

## 7. 新增文件一览

- 界面：`GameUi.CartSide.cs`、`GameUi.HudSlim.cs`、`GameUi.OnlineParcels.cs`、`GameUi.OpeningMeal.cs`、`GameUi.ProcurementShops.cs`、`GameUi.HomeWorkbenchKit.cs`、`GameUi.HomeWorkbenchBusiness.cs`、`Ui.FlatSkin.cs`。
- 编辑器：`GameSceneBuilder.CookingTutorial.cs`、`GameSceneBuilder.HomeWorkbenchView.cs`、`GameSceneBuilder.PurchaseAudit.cs`、`GameSceneBuilder.StockView.cs`、`GameSceneBuilder.UiAudit.cs`。
