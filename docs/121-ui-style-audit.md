# UI 样式巡查归档

本文保留当时观察到的界面差异和修改提案。统一样式方案曾撤回，后来落地的防闪烁、控件焦点和结算动效见[当前更新](123-playtest-update-2026-11-17.md#显示声音出行和存档)。

范围为当前 Unity 主工程 `8c26247` 的游戏界面。静态遍历了 `Assets/Scripts/Game/UI` 下 124 个 C# 文件，其中有 97 个文件调用通用按钮。用 1600 × 900 的 Unity 渲染复核了选择目的地、十街地图、采购与补货、日程、证券、手机桌面、垂钓买时长和普通邀约。中介地图、门店和其余条件分支的记录来自源码及已有实景反馈，未逐个进入所有经营阶段。此次交付为审查清单。

## 以目的地原型为准

普通按钮用深蓝底和细蓝灰边。页签、筛选项和列表的选中态用较亮的深灰蓝底、金色细边，必要时加金字。金棕实心留给当前明确的主操作，例如「立即前往」「确认付款」「开始活动」。标题栏关闭按钮保留原型的赤棕底；图标按钮、禁用、悬停和按下各自有稳定的反馈。日历选中日期和证券底部的页签可作为现有游戏内对照。见[目的地原型](previews/destination-picker.html)。

### 共用组件的三个问题

1. **样式依赖按钮数量和文案。** 窗口建完后会再扫描水平按钮组。只有 3–6 个按钮、一个金色、短文案、可伸展宽度等条件同时满足，才会自动改成紧凑的深蓝金字页签。双项切换、超过六项的列表、混入标签以及文案变长时会保留原样。改用明确声明的「页签／筛选／列表选中」控件。 [自动处理规则](../Assets/Scripts/Game/UI/GameUi.HudSlim.cs#L35) · [调用入口](../Assets/Scripts/Game/UI/GameUi.cs#L49)
2. **状态切换多处只写底色。** 部分按钮创建时确定描边，切换后只修改 `image.color`；也有页面自己维护文字色。应由同一个样式入口更新底色、边框、文字／图标和悬停、禁用状态。 [共用皮肤](../Assets/Scripts/Game/UI/Ui.FlatSkin.cs#L30) · [通用按钮](../Assets/Scripts/Game/UI/Ui.cs#L136)
3. **可点击卡片和普通按钮走不同入口。** 头像、统计、表格行和场景热点有直接挂 Unity Button 的实现。带文字的入口、可选列表行与可点击卡片需要统一外框和交互反馈。图片热点保留点击区域，用可见的悬停／选中提示让玩家知道哪里能点。 [门店统计卡](../Assets/Scripts/Game/UI/GameUi.Shop.cs#L478) · [人物头像](../Assets/Scripts/Game/UI/GameUi.Npc.cs#L75)

## 实景里优先修改的页面

| 页面 | 看见的不一致 | 具体改法 |
| --- | --- | --- |
| 自定义目的地 | 布局已按原型落地，选中整行却刷成金棕，描边仍取创建时的普通色。右侧「立即前往」也是金棕，选中与出发看起来是一种操作。 | 左侧选中行改为灰蓝底＋金边，右侧出发保留金棕，保存为深蓝次按钮。 [选中状态](../Assets/Scripts/Game/UI/GameUi.CustomDestination.cs#L163) |
| 十街地图 | 类型和第一排街区缩成页签，第二排街区仍铺满整行。结果又用居中的大按钮，和新版选择器差异明显。 | 街区控制统一为下拉或同规格筛选项，地图内保留原有地点标记，地图下的结果统一为两行左对齐。 [地图筛选及结果](../Assets/Scripts/Game/UI/GameUi.CityLifePanels.cs#L68) |
| 证券 | 同页出现全宽金色「现货交易」、底部深蓝金字「交易所」、实心「1手」。报价表里「买入」和「最大买入」两列都抢主操作的视觉重量。 | 交易模式、页面和手数都用同一选中态。保留表格紧凑尺寸，区分常规下单与批量快捷操作的强调级别。 [页签与手数](../Assets/Scripts/Game/UI/GameUi.StockPhone.cs#L152) · [交易按钮](../Assets/Scripts/Game/UI/GameUi.StockPhone.cs#L329) |
| 普通随机事件及邀约 | 实测周国良邀约中的 AA、我请、改天再约、这次不参加四项都是金色。其他普通事件沿用同一渲染器。夜宵的新预约弹窗已有套餐和付款选择，但选中项与确认仍用同一种金棕。 | 选项和改天再约用普通按钮，当前选择用金边，可明确继续时强调确认。保留事件原有选择和后果。 [普通选项与预约](../Assets/Scripts/Game/UI/GameUi.cs#L1032) |
| 垂钓、篮球、高尔夫、赛车练习 | 垂钓的四档时长都是占满卡片的实心金色，篮球和高尔夫共用这套买时长控件，赛车练习另有类似实现。 | 价格和时长保持醒目，整张卡用深蓝细边，明确的购买动作使用主按钮；预约仅有一项时可直接突出开场。 [场馆时长](../Assets/Scripts/Game/UI/GameUi.VenueServices.cs#L71) · [赛车练习](../Assets/Scripts/Game/UI/GameUi.Racing.cs#L509) |
| 手机桌面 | 应用图标背后是蓝绿橙红等饱和大色块，和应用商店、顶部导航及同一壳里的其他界面差异明显。 | 应用入口统一为同尺寸的深蓝细边方块，色彩用于图标识别，借款、证券、通讯录仍可快速区分。 [手机桌面](../Assets/Scripts/Game/UI/GameUi.Phone.cs#L30) |
| 采购与补货 | 补货清单及缺货、在途已经有深蓝填充层级，但边框和文字对选中的提示偏轻；同系统的安排进货有其他选中样式。 | 对齐筛选、次操作和「添加商品／完成」的层级，商品列表及数量输入继续使用紧凑尺寸。 [补货筛选](../Assets/Scripts/Game/UI/GameUi.ProcurementShops.cs#L141) · [员工采购](../Assets/Scripts/Game/UI/GameUi.Employees.cs#L283) |

## 其余页面的改动清单

| 页面 | 要统一的控件 | 源码入口 |
| --- | --- | --- |
| 顶栏、底栏、房间提示 | 暂停／倍速、出行方式、高亮快捷位、店铺快捷栏小三角；接下来的安排是透明可点行，和任务／还款卡片对齐悬停、边框、焦点。 | [倍速](../Assets/Scripts/Game/UI/GameUi.cs#L353) · [出行](../Assets/Scripts/Game/UI/GameUi.cs#L418) · [快捷按钮](../Assets/Scripts/Game/UI/GameUi.CityNavigation.cs#L72) · [接下来的安排](../Assets/Scripts/Game/UI/GameUi.UpcomingSchedule.cs#L31) |
| 店铺、二手收购、员工 | 店铺顶部页签切换只换颜色；可点的营业额、人流统计卡外形接近纯信息卡；二手台页签、代管切换和员工「展开详情」的强调级别不同。统一页签和按钮状态，给可点统计卡一致的外框。 | [门店页签](../Assets/Scripts/Game/UI/GameUi.Shop.cs#L178) · [统计卡](../Assets/Scripts/Game/UI/GameUi.Shop.cs#L478) · [二手代管](../Assets/Scripts/Game/UI/GameUi.Resale.cs#L74) · [员工](../Assets/Scripts/Game/UI/GameUi.Employees.cs#L83) |
| 商品购买、网购、叫货车、背包 | 购物车「送到」是自定灰色模拟下拉，其他位置用标准下拉；已选商品行、数量加减、批量入库、装车和结账跨页面层级不同。统一目的地下拉和紧凑加减按钮。 | [购物车目的地](../Assets/Scripts/Game/UI/GameUi.Cart.cs#L225) · [数量](../Assets/Scripts/Game/UI/GameUi.Quantities.cs) · [网购](../Assets/Scripts/Game/UI/GameUi.OnlineParcels.cs#L83) · [叫货车](../Assets/Scripts/Game/UI/GameUi.Panels.cs) |
| 银行与贷款 | 银行「存取款／借款／我的贷款」依赖窗口的自动页签处理，页内「存款／取款」另有显式画法。统一同类切换，申请借款和提前结清继续按动作层级与资格反馈。 | [银行页签](../Assets/Scripts/Game/UI/GameUi.Bank.cs#L74) · [存取款](../Assets/Scripts/Game/UI/GameUi.Bank.cs#L101) · [贷款](../Assets/Scripts/Game/UI/GameUi.Loans.cs#L169) |
| 中介、资产、住宅及园区 | 商铺／住宅、租赁／全款是成对金色填充切换；资产的区域按钮和物业列表还会只换底色；地图上绿色空铺、橙色待交房等状态另有用途。统一工具条、页签和选中描边，保留地图的状态识别。 | [商铺与住宅](../Assets/Scripts/Game/UI/GameUi.Agency.cs#L32) · [租购模式](../Assets/Scripts/Game/UI/GameUi.Agency.cs#L125) · [资产地图](../Assets/Scripts/Game/UI/GameUi.AssetProperties.cs#L68) |
| 车库、车行、改装与赛场报名 | 车辆类目会受数量和文案影响，有时紧凑、有时整排金色；「用于通勤／停入车库」也和正式购买并用金色。统一类目、赛历行及车辆选择状态，将登记、购买和报名作为对应页面的主操作。 | [车型页签](../Assets/Scripts/Game/UI/GameUi.RegionalGarages.cs#L27) · [购车用途](../Assets/Scripts/Game/UI/GameUi.RegionalGarages.cs#L275) · [赛历](../Assets/Scripts/Game/UI/GameUi.Racing.cs#L240) |
| 品牌、MCN、直播、展会 | 品牌多页签、旗下门店、MCN 公司、主播筛选、展位选择各自声明选中颜色。统一这些选择控件，海报、开播状态和直播视频继续承载内容识别。 | [品牌](../Assets/Scripts/Game/UI/GameUi.Brands.cs#L40) · [MCN](../Assets/Scripts/Game/UI/GameUi.Mcn.cs#L42) · [直播](../Assets/Scripts/Game/UI/GameUi.KolViewer.cs#L64) · [展会](../Assets/Scripts/Game/UI/GameUi.ExpoOverview.cs#L41) |
| 人物、关系图、联系人、详情对话 | 店内、关系图、名片及 KOL 头像的悬停方案不同，部分对话选项用各自的禁用色。统一头像外框交互和文本选项；对话纸面与头像继续承载人物画面。 | [头像](../Assets/Scripts/Game/UI/GameUi.Npc.cs#L75) · [关系图头像](../Assets/Scripts/Game/UI/GameUi.Relations.cs#L128) · [对话选项](../Assets/Scripts/Game/UI/GameUi.Npc.cs#L388) |
| 居家办公台、家庭与生活管理 | 办公台已有深色页签和展开行，报表和价格历史却还有透明可点击行。给可点行统一悬停／边框，用办公台现有紧凑页签作为并入共用控件的参考。 | [办公台页签](../Assets/Scripts/Game/UI/GameUi.HomeWorkbench.cs#L135) · [报表行](../Assets/Scripts/Game/UI/GameUi.HomeWorkbenchBusiness.cs#L452) |
| 日程、任务、通知、系统菜单 | 日历日期格已经有合适的金细边。日历与任务跳转、通知条、通用弹窗里的返回／关闭／确认以及设置里的开关继续对齐同一组角色；一般确认用金棕，带实际损失或退出的确认用风险色。 | [日历格](../Assets/Scripts/Game/UI/GameUi.MonthCalendar.cs#L85) · [任务操作](../Assets/Scripts/Game/UI/GameUi.Quests.cs#L63) · [主菜单](../Assets/Scripts/Game/UI/GameUi.MainMenu.cs#L64) · [通用确认框](../Assets/Scripts/Game/UI/GameUi.Tip.cs#L124) |
| 小游戏、现场操作、装修与教程 | 手动投篮、抛竿、装机的主操作可沿用金棕。饵料选择、器材、工具与局内次按钮统一边框和禁用状态；保留时机、力度、进度及错误的专用反馈。 | [垂钓控件](../Assets/Scripts/Game/UI/GameUi.Fishing.cs#L125) · [现场动作](../Assets/Scripts/Game/UI/GameUi.OnsitePlay.cs#L129) · [装机](../Assets/Scripts/Game/UI/BuildMinigame.cs#L264) |

另外，调价、音量和其他共用滑块现在是白块手柄，和搜索框、下拉框的深蓝样式分属两套。适合随基础控件一起对齐，但优先级低于按钮状态。 [滑块实现](../Assets/Scripts/Game/UI/Ui.cs#L353)

实施顺序建议先统一四类角色及状态，再替换自动猜测页签。第一批优先覆盖证券、普通事件、场馆买时长、目的地及十街地图、手机桌面，第二批再迁移顶部／底部、店铺、采购、银行和其他场景。用同一组视图检查普通、选中、主操作、关闭、禁用在触发前后的表现。
